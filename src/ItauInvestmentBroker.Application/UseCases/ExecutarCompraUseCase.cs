using ItauInvestmentBroker.Application.DTOs.Motor;
using ItauInvestmentBroker.Application.Exceptions;
using ItauInvestmentBroker.Application.Interfaces;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Enums;
using ItauInvestmentBroker.Domain.Repositories;

namespace ItauInvestmentBroker.Application.UseCases;

public class ExecutarCompraUseCase(
    ICestaRepository cestaRepository,
    IClienteRepository clienteRepository,
    IContaGraficaRepository contaGraficaRepository,
    ICustodiaRepository custodiaRepository,
    IOrdemCompraRepository ordemCompraRepository,
    IDistribuicaoRepository distribuicaoRepository,
    ICotacaoService cotacaoService,
    IKafkaProducer kafkaProducer,
    IUnitOfWork unitOfWork)
{
    private const decimal DivisorMensal = 3m;
    private const int LotePadrao = 100;
    private const decimal AliquotaIrDedoDuro = 0.00005m;
    private const string TopicoIrDedoDuro = "ir-dedo-duro";

    public async Task<ExecutarCompraResponse> Executar(CancellationToken cancellationToken = default)
    {
        // 1. Carregar cesta ativa + clientes ativos + conta master
        var cesta = await cestaRepository.FindAtiva(cancellationToken)
            ?? throw new NotFoundException("Nenhuma cesta ativa encontrada.", ErrorCodes.CestaNaoEncontrada);

        var clientes = (await clienteRepository.FindAtivos(cancellationToken)).ToList();
        if (clientes.Count == 0)
            throw new BusinessException("Nenhum cliente ativo encontrado.", ErrorCodes.ClienteNaoEncontrado);

        var contasMaster = (await contaGraficaRepository.FindMaster(cancellationToken)).ToList();
        var contaMaster = contasMaster.FirstOrDefault()
            ?? throw new NotFoundException("Conta master não encontrada.", ErrorCodes.ClienteNaoEncontrado);

        // 2. Calcular valor total a investir (1/3 do valor mensal de cada cliente)
        var valorTotalInvestimento = clientes.Sum(c => c.ValorMensal / DivisorMensal);

        // 3. Para cada ticker: buscar cotação, saldo master, calcular quantidade
        var itensCompra = new List<ItemCompraInfo>();

        foreach (var itemCesta in cesta.Itens)
        {
            var cotacao = cotacaoService.ObterCotacao(itemCesta.Ticker)
                ?? throw new BusinessException(
                    $"Cotação não encontrada para o ticker {itemCesta.Ticker}.",
                    ErrorCodes.CotacaoNaoEncontrada);

            var valorParaTicker = valorTotalInvestimento * (itemCesta.Percentual / 100m);
            var quantidadeNecessaria = (int)(valorParaTicker / cotacao.PrecoFechamento);

            // RN-029/030: Descontar saldo da custódia master
            var custodiaMaster = await custodiaRepository.FindByContaGraficaIdAndTicker(
                contaMaster.Id, itemCesta.Ticker, cancellationToken);
            var saldoMaster = custodiaMaster?.Quantidade ?? 0;

            var quantidadeAComprar = Math.Max(0, quantidadeNecessaria - saldoMaster);

            // RN-037: Total disponível = compradas + saldo master
            var quantidadeTotalDisponivel = quantidadeAComprar + saldoMaster;

            if (quantidadeTotalDisponivel == 0)
                continue;

            // RN-031/032: Separar lote padrão vs fracionário
            var quantidadeLote = (quantidadeAComprar / LotePadrao) * LotePadrao;
            var quantidadeFracionario = quantidadeAComprar - quantidadeLote;

            itensCompra.Add(new ItemCompraInfo
            {
                Ticker = itemCesta.Ticker,
                Percentual = itemCesta.Percentual,
                PrecoUnitario = cotacao.PrecoFechamento,
                QuantidadeLote = quantidadeLote,
                QuantidadeFracionario = quantidadeFracionario,
                QuantidadeAComprar = quantidadeAComprar,
                QuantidadeTotalDisponivel = quantidadeTotalDisponivel,
                SaldoMasterAnterior = saldoMaster
            });
        }

        // 4. Criar OrdemCompra na conta master
        var ordem = new OrdemCompra
        {
            DataExecucao = DateTime.UtcNow,
            Status = StatusOrdemCompra.EXECUTADA,
            ValorTotal = itensCompra.Sum(i => i.QuantidadeAComprar * i.PrecoUnitario),
            ContaGraficaId = contaMaster.Id,
            CestaId = cesta.Id
        };

        foreach (var item in itensCompra)
        {
            if (item.QuantidadeLote > 0)
            {
                ordem.Itens.Add(new ItemOrdemCompra
                {
                    Ticker = item.Ticker,
                    Quantidade = item.QuantidadeLote,
                    PrecoUnitario = item.PrecoUnitario,
                    ValorTotal = item.QuantidadeLote * item.PrecoUnitario,
                    TipoMercado = TipoMercado.LOTE
                });
            }

            if (item.QuantidadeFracionario > 0)
            {
                ordem.Itens.Add(new ItemOrdemCompra
                {
                    Ticker = item.Ticker + "F",
                    Quantidade = item.QuantidadeFracionario,
                    PrecoUnitario = item.PrecoUnitario,
                    ValorTotal = item.QuantidadeFracionario * item.PrecoUnitario,
                    TipoMercado = TipoMercado.FRACIONARIO
                });
            }
        }

        ordemCompraRepository.Add(ordem);

        // 5. Zerar custódia master dos tickers (será recalculada com resíduos)
        foreach (var item in itensCompra)
        {
            if (item.SaldoMasterAnterior > 0)
            {
                var custodiaMaster = await custodiaRepository.FindByContaGraficaIdAndTicker(
                    contaMaster.Id, item.Ticker, cancellationToken);
                if (custodiaMaster is not null)
                    custodiaMaster.Quantidade = 0;
            }
        }

        // 6. Distribuir proporcionalmente para contas filhotes
        var distribuicao = new Distribuicao
        {
            OrdemCompraId = ordem.Id,
            OrdemCompra = ordem
        };

        var eventosIr = new List<IrDedoDuroEvent>();

        foreach (var item in itensCompra)
        {
            var quantidadeDistribuida = 0;

            foreach (var cliente in clientes)
            {
                // RN-035: Proporção = aporte do cliente / total de aportes
                var proporcao = (cliente.ValorMensal / DivisorMensal) / valorTotalInvestimento;
                // RN-036: Quantidade = TRUNCAR(proporção × total disponível)
                var quantidadeCliente = (int)(item.QuantidadeTotalDisponivel * proporcao);

                if (quantidadeCliente == 0)
                    continue;

                quantidadeDistribuida += quantidadeCliente;

                distribuicao.Itens.Add(new ItemDistribuicao
                {
                    Ticker = item.Ticker,
                    Quantidade = quantidadeCliente,
                    ContaGraficaId = cliente.ContaGrafica!.Id
                });

                // 7. Atualizar custódia filhote + preço médio
                await AtualizarCustodia(
                    cliente.ContaGrafica.Id,
                    item.Ticker,
                    quantidadeCliente,
                    item.PrecoUnitario,
                    cancellationToken);

                // RN-054/056: IR dedo-duro por cliente por ticker
                var valorOperacao = quantidadeCliente * item.PrecoUnitario;
                var valorIr = Math.Round(valorOperacao * AliquotaIrDedoDuro, 2);

                eventosIr.Add(new IrDedoDuroEvent(
                    Tipo: "IR_DEDO_DURO",
                    ClienteId: cliente.Id,
                    Cpf: cliente.Cpf,
                    Ticker: item.Ticker,
                    TipoOperacao: "COMPRA",
                    Quantidade: quantidadeCliente,
                    PrecoUnitario: item.PrecoUnitario,
                    ValorOperacao: valorOperacao,
                    Aliquota: AliquotaIrDedoDuro,
                    ValorIR: valorIr,
                    DataOperacao: ordem.DataExecucao
                ));
            }

            // 8. Resíduo fica na conta master (RN-039)
            var residuo = item.QuantidadeTotalDisponivel - quantidadeDistribuida;
            if (residuo > 0)
            {
                distribuicao.Itens.Add(new ItemDistribuicao
                {
                    Ticker = item.Ticker,
                    Quantidade = residuo,
                    ContaGraficaId = contaMaster.Id
                });

                await AtualizarCustodia(
                    contaMaster.Id,
                    item.Ticker,
                    residuo,
                    item.PrecoUnitario,
                    cancellationToken);
            }
        }

        distribuicaoRepository.Add(distribuicao);

        // 9. Commit da transação
        await unitOfWork.CommitAsync(cancellationToken);

        // 10. Publicar IR dedo-duro no Kafka (após commit)
        foreach (var evento in eventosIr)
        {
            await kafkaProducer.ProduceAsync(
                TopicoIrDedoDuro,
                $"{evento.ClienteId}-{evento.Ticker}",
                evento);
        }

        return new ExecutarCompraResponse(
            ordem.Id,
            ordem.DataExecucao,
            ordem.ValorTotal,
            clientes.Count,
            itensCompra.Select(i => new ItemCompraResponse(
                i.Ticker,
                i.QuantidadeLote,
                i.QuantidadeFracionario,
                i.QuantidadeAComprar,
                i.PrecoUnitario,
                i.QuantidadeAComprar * i.PrecoUnitario
            )).ToList()
        );
    }

    private async Task AtualizarCustodia(
        long contaGraficaId,
        string ticker,
        int quantidadeNova,
        decimal precoCompra,
        CancellationToken cancellationToken)
    {
        var custodia = await custodiaRepository.FindByContaGraficaIdAndTicker(
            contaGraficaId, ticker, cancellationToken);

        if (custodia is null)
        {
            custodia = new Custodia
            {
                Ticker = ticker,
                Quantidade = quantidadeNova,
                PrecoMedio = precoCompra,
                ContaGraficaId = contaGraficaId,
                DataUltimaAtualizacao = DateTime.UtcNow
            };
            custodiaRepository.Add(custodia);
        }
        else
        {
            // RN-042: PM = (Qtd Anterior × PM Anterior + Qtd Nova × Preço Nova) / (Qtd Anterior + Qtd Nova)
            custodia.PrecoMedio =
                (custodia.Quantidade * custodia.PrecoMedio + quantidadeNova * precoCompra)
                / (custodia.Quantidade + quantidadeNova);
            custodia.Quantidade += quantidadeNova;
            custodia.DataUltimaAtualizacao = DateTime.UtcNow;
        }
    }

    private class ItemCompraInfo
    {
        public string Ticker { get; init; } = string.Empty;
        public decimal Percentual { get; init; }
        public decimal PrecoUnitario { get; init; }
        public int QuantidadeLote { get; init; }
        public int QuantidadeFracionario { get; init; }
        public int QuantidadeAComprar { get; init; }
        public int QuantidadeTotalDisponivel { get; init; }
        public int SaldoMasterAnterior { get; init; }
    }
}
