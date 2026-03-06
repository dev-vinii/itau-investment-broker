using ItauInvestmentBroker.Application.Common.Configuration;
using ItauInvestmentBroker.Application.Features.Motor.DTOs;
using ItauInvestmentBroker.Application.Common.Exceptions;
using ItauInvestmentBroker.Application.Common.Interfaces;
using ItauInvestmentBroker.Application.Services;
using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Motor.Entities;
using ItauInvestmentBroker.Domain.Clientes.Enums;
using ItauInvestmentBroker.Domain.Motor.Enums;
using ItauInvestmentBroker.Domain.Cestas.Repositories;
using ItauInvestmentBroker.Domain.Clientes.Repositories;
using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Domain.Motor.Repositories;
using Microsoft.Extensions.Options;

namespace ItauInvestmentBroker.Application.Features.Motor.UseCases;

public class ExecutarCompraUseCase(
    ICestaRepository cestaRepository,
    IClienteRepository clienteRepository,
    IContaGraficaRepository contaGraficaRepository,
    ICustodiaRepository custodiaRepository,
    IOrdemCompraRepository ordemCompraRepository,
    IDistribuicaoRepository distribuicaoRepository,
    ICotacaoService cotacaoService,
    CustodiaAppService custodiaAppService,
    IrCalculationService irCalculationService,
    KafkaEventPublisher kafkaEventPublisher,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork,
    IOptions<MotorSettings> motorSettings)
{
    private readonly MotorSettings _settings = motorSettings.Value;

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
            ?? throw new NotFoundException("Conta master nao encontrada.", ErrorCodes.ClienteNaoEncontrado);

        // RN-023/RN-025/RN-026: Aporte do ciclo = valor mensal / divisor, consolidado para compra unica.
        var valorTotalInvestimento = clientes.Sum(c => c.ValorMensal / _settings.DivisorMensal);

        var itensCompra = await CalcularItensCompra(cesta, contaMaster, valorTotalInvestimento, cancellationToken);

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var dataExecucao = dateTimeProvider.UtcNow;
            var ordem = CriarOrdemCompra(itensCompra, contaMaster.Id, cesta.Id, dataExecucao);
            ordemCompraRepository.Add(ordem);

            // RN-039: Zerar saldo master consumido.
            await ZerarSaldoMasterConsumido(itensCompra, contaMaster.Id, cancellationToken);

            // RN-034: Distribuicao para filhotes proporcional ao aporte.
            var (distribuicao, eventosIr) = await DistribuirParaClientes(
                ordem, itensCompra, clientes, contaMaster, valorTotalInvestimento, cancellationToken);
            distribuicaoRepository.Add(distribuicao);

            await unitOfWork.CommitTransactionAsync(cancellationToken);

            // RN-055: Publicar eventos de IR apos persistencia.
            await kafkaEventPublisher.PublicarEventosIr(eventosIr, []);

            return new ExecutarCompraResponse(
                ordem.Id, ordem.DataExecucao, ordem.ValorTotal, clientes.Count,
                itensCompra.Select(i => new ItemCompraResponse(
                    i.Ticker, i.QuantidadeLote, i.QuantidadeFracionario,
                    i.QuantidadeAComprar, i.PrecoUnitario,
                    i.QuantidadeAComprar * i.PrecoUnitario
                )).ToList());
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task<List<ItemCompraInfo>> CalcularItensCompra(
        Cesta cesta, ContaGrafica contaMaster, decimal valorTotalInvestimento,
        CancellationToken cancellationToken)
    {
        var itensCompra = new List<ItemCompraInfo>();

        foreach (var itemCesta in cesta.Itens)
        {
            var cotacao = cotacaoService.ObterCotacao(itemCesta.Ticker)
                ?? throw new BusinessException(
                    $"Cotacao nao encontrada para o ticker {itemCesta.Ticker}.",
                    ErrorCodes.CotacaoNaoEncontrada);

            // RN-028: Quantidade calculada com truncamento (inteiro para baixo).
            var valorParaTicker = valorTotalInvestimento * (itemCesta.Percentual / 100m);
            var quantidadeNecessaria = (int)(valorParaTicker / cotacao.PrecoFechamento);

            // RN-029/RN-030/RN-040: Considerar saldo master e descontar da nova compra.
            var custodiaMaster = await custodiaRepository.FindByContaGraficaIdAndTicker(
                contaMaster.Id, itemCesta.Ticker, cancellationToken);
            var saldoMaster = custodiaMaster?.Quantidade ?? 0;

            var quantidadeAComprar = Math.Max(0, quantidadeNecessaria - saldoMaster);
            var quantidadeTotalDisponivel = quantidadeAComprar + saldoMaster;

            if (quantidadeTotalDisponivel == 0)
                continue;

            // RN-031/RN-032: Separar mercado de lote e fracionario.
            var quantidadeLote = (quantidadeAComprar / _settings.LotePadrao) * _settings.LotePadrao;
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

        return itensCompra;
    }

    private OrdemCompra CriarOrdemCompra(
        List<ItemCompraInfo> itensCompra, long contaMasterId, long cestaId, DateTime dataExecucao)
    {
        var ordem = new OrdemCompra
        {
            DataExecucao = dataExecucao,
            Status = StatusOrdemCompra.EXECUTADA,
            ValorTotal = itensCompra.Sum(i => i.QuantidadeAComprar * i.PrecoUnitario),
            ContaGraficaId = contaMasterId,
            CestaId = cestaId
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

        return ordem;
    }

    private async Task ZerarSaldoMasterConsumido(
        List<ItemCompraInfo> itensCompra, long contaMasterId, CancellationToken cancellationToken)
    {
        foreach (var item in itensCompra)
        {
            if (item.SaldoMasterAnterior > 0)
            {
                var custodiaMaster = await custodiaRepository.FindByContaGraficaIdAndTicker(
                    contaMasterId, item.Ticker, cancellationToken);
                if (custodiaMaster is not null)
                    custodiaMaster.Quantidade = 0;
            }
        }
    }

    private async Task<(Distribuicao, List<IrDedoDuroEvent>)> DistribuirParaClientes(
        OrdemCompra ordem, List<ItemCompraInfo> itensCompra, List<Cliente> clientes,
        ContaGrafica contaMaster, decimal valorTotalInvestimento, CancellationToken cancellationToken)
    {
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
                // RN-035: Proporcao = aporte do cliente / total de aportes
                var proporcao = (cliente.ValorMensal / _settings.DivisorMensal) / valorTotalInvestimento;
                // RN-036: Quantidade = TRUNCAR(proporcao x total disponivel)
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

                // RN-038/RN-041/RN-044: Atualizar custodia e recalcular preco medio.
                await custodiaAppService.AtualizarCustodia(
                    cliente.ContaGrafica.Id, item.Ticker, quantidadeCliente,
                    item.PrecoUnitario, cancellationToken);

                // RN-053/RN-054/RN-056: Calcular IR dedo-duro por operacao distribuida.
                eventosIr.Add(irCalculationService.CalcularIrDedoDuro(
                    cliente.Id, cliente.Cpf, item.Ticker, "COMPRA",
                    quantidadeCliente, item.PrecoUnitario, ordem.DataExecucao));
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

                await custodiaAppService.AtualizarCustodia(
                    contaMaster.Id, item.Ticker, residuo,
                    item.PrecoUnitario, cancellationToken);
            }
        }

        return (distribuicao, eventosIr);
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
