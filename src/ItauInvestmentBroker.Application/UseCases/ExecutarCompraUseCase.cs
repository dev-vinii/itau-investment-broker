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
        // RN-026: Execucao da compra usa a cesta ativa para consolidacao.
        var cesta = await cestaRepository.FindAtiva(cancellationToken)
            ?? throw new NotFoundException("Nenhuma cesta ativa encontrada.", ErrorCodes.CestaNaoEncontrada);

        // RN-024: Apenas clientes ativos participam da compra programada.
        var clientes = (await clienteRepository.FindAtivos(cancellationToken)).ToList();
        if (clientes.Count == 0)
            throw new BusinessException("Nenhum cliente ativo encontrado.", ErrorCodes.ClienteNaoEncontrado);

        var contasMaster = (await contaGraficaRepository.FindMaster(cancellationToken)).ToList();
        var contaMaster = contasMaster.FirstOrDefault()
            ?? throw new NotFoundException("Conta master não encontrada.", ErrorCodes.ClienteNaoEncontrado);

        // RN-023/RN-025/RN-026: Aporte do ciclo = valor mensal / 3, consolidado para compra unica.
        var valorTotalInvestimento = clientes.Sum(c => c.ValorMensal / DivisorMensal);

        // 3. Para cada ticker: buscar cotação, saldo master, calcular quantidade
        var itensCompra = new List<ItemCompraInfo>();

        foreach (var itemCesta in cesta.Itens)
        {
            var cotacao = cotacaoService.ObterCotacao(itemCesta.Ticker)
                ?? throw new BusinessException(
                    $"Cotação não encontrada para o ticker {itemCesta.Ticker}.",
                    ErrorCodes.CotacaoNaoEncontrada);

            // RN-028: Quantidade calculada com truncamento (inteiro para baixo).
            var valorParaTicker = valorTotalInvestimento * (itemCesta.Percentual / 100m);
            var quantidadeNecessaria = (int)(valorParaTicker / cotacao.PrecoFechamento);

            // RN-029/RN-030/RN-040: Considerar saldo master e descontar da nova compra.
            var custodiaMaster = await custodiaRepository.FindByContaGraficaIdAndTicker(
                contaMaster.Id, itemCesta.Ticker, cancellationToken);
            var saldoMaster = custodiaMaster?.Quantidade ?? 0;

            var quantidadeAComprar = Math.Max(0, quantidadeNecessaria - saldoMaster);

            // RN-037: Total disponível = compradas + saldo master
            var quantidadeTotalDisponivel = quantidadeAComprar + saldoMaster;

            if (quantidadeTotalDisponivel == 0)
                continue;

            // RN-031/RN-032: Separar mercado de lote e fracionario.
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
                    // RN-033: Ticker do fracionario usa sufixo "F".
                    Ticker = item.Ticker + "F",
                    Quantidade = item.QuantidadeFracionario,
                    PrecoUnitario = item.PrecoUnitario,
                    ValorTotal = item.QuantidadeFracionario * item.PrecoUnitario,
                    TipoMercado = TipoMercado.FRACIONARIO
                });
            }
        }

        ordemCompraRepository.Add(ordem);

            // RN-039: Residuos permanecem na master apos redistribuicao.
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

        // RN-034: Distribuicao para filhotes e proporcional ao aporte do cliente.
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

                // RN-038/RN-041/RN-044: Atualizar custodia e recalcular preco medio em compras.
                await AtualizarCustodia(
                    cliente.ContaGrafica.Id,
                    item.Ticker,
                    quantidadeCliente,
                    item.PrecoUnitario,
                    cancellationToken);

                // RN-053/RN-054/RN-056: Calcular IR dedo-duro por operacao distribuida.
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

            // RN-039: Residuo nao distribuido permanece em custodia master.
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

        // RN-055: Publicacao Kafka deve ocorrer apos persistencia da operacao.
        await unitOfWork.CommitAsync(cancellationToken);

        // RN-055: Publicar eventos de IR dedo-duro no Kafka.
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
            // RN-042: PM = (Qtd Anterior x PM Anterior + Qtd Nova x Preco Novo) / (Qtd Anterior + Qtd Nova)
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
