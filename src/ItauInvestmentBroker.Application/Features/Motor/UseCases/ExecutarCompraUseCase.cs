using ItauInvestmentBroker.Application.Common.Configuration;
using ItauInvestmentBroker.Application.Common.Constants;
using ItauInvestmentBroker.Application.Features.Motor.DTOs;
using ItauInvestmentBroker.Application.Common.Exceptions;
using ItauInvestmentBroker.Application.Common.Interfaces;
using ItauInvestmentBroker.Application.Common.Utils;
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
using Microsoft.Extensions.Logging;
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
    IrCalculationService irCalculationService,
    KafkaEventPublisher kafkaEventPublisher,
    ILogger<ExecutarCompraUseCase> logger,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork,
    IOptions<MotorSettings> motorSettings)
{
    private readonly MotorSettings _settings = motorSettings.Value;
    private const string OperacaoExecutarCompra = "EXECUTAR_COMPRA";

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
        var custodiasCache = await CarregarCustodiasParaDistribuicao(clientes, contaMaster, itensCompra, cancellationToken);

        using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var dataExecucao = dateTimeProvider.UtcNow;
            var ordem = CriarOrdemCompra(itensCompra, contaMaster.Id, cesta.Id, dataExecucao);
            ordemCompraRepository.Add(ordem);

            // RN-039: Zerar saldo master consumido.
            ZerarSaldoMasterConsumido(itensCompra, contaMaster.Id, custodiasCache);

            // RN-034: Distribuicao para filhotes proporcional ao aporte.
            var (distribuicao, eventosIr) = DistribuirParaClientes(
                ordem, itensCompra, clientes, contaMaster, valorTotalInvestimento, custodiasCache);
            distribuicaoRepository.Add(distribuicao);

            await unitOfWork.CommitTransactionAsync(cancellationToken);

            var ordemCompraExecutadaPublicada = await kafkaEventPublisher.PublicarOrdemCompraExecutada(new OrdemCompraExecutadaEvent(
                Tipo: KafkaEventTypes.OrdemCompraExecutada,
                OrdemCompraId: ordem.Id,
                CestaId: cesta.Id,
                ContaGraficaMasterId: contaMaster.Id,
                TotalClientes: clientes.Count,
                ValorTotal: ordem.ValorTotal,
                DataExecucao: ordem.DataExecucao));
            if (!ordemCompraExecutadaPublicada)
                logger.LogError("Evento critico nao publicado: {Tipo} para ordem {OrdemCompraId}", KafkaEventTypes.OrdemCompraExecutada, ordem.Id);

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
        catch (Exception ex)
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);

            try
            {
                var motorExecucaoFalhouPublicado = await kafkaEventPublisher.PublicarMotorExecucaoFalhou(new MotorExecucaoFalhouEvent(
                    Tipo: KafkaEventTypes.MotorExecucaoFalhou,
                    Operacao: OperacaoExecutarCompra,
                    Erro: ex.Message,
                    CodigoErro: ObterCodigoErro(ex),
                    DataOcorrencia: dateTimeProvider.UtcNow));
                if (!motorExecucaoFalhouPublicado)
                    logger.LogError("Evento critico nao publicado: {Tipo} para operacao {Operacao}", KafkaEventTypes.MotorExecucaoFalhou, OperacaoExecutarCompra);
            }
            catch
            {
                // O erro original da execução do motor deve prevalecer.
            }

            throw;
        }
    }

    private static string? ObterCodigoErro(Exception ex)
    {
        return ex switch
        {
            BusinessException businessException => businessException.Codigo,
            _ => null
        };
    }

    private async Task<Dictionary<(long ContaGraficaId, string Ticker), Custodia>> CarregarCustodiasParaDistribuicao(
        List<Cliente> clientes,
        ContaGrafica contaMaster,
        List<ItemCompraInfo> itensCompra,
        CancellationToken cancellationToken)
    {
        var contaIds = clientes
            .Select(c => c.ContaGrafica?.Id)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Append(contaMaster.Id)
            .Distinct()
            .ToList();

        var tickers = itensCompra
            .Select(i => i.Ticker)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var custodias = (await custodiaRepository.FindByContaGraficaIdsAndTickers(contaIds, tickers, cancellationToken)
                        ?? Enumerable.Empty<Custodia>())
            .ToDictionary(c => (c.ContaGraficaId, TickerUtils.Normalize(c.Ticker)), c => c);

        return custodias;
    }

    private async Task<List<ItemCompraInfo>> CalcularItensCompra(
        Cesta cesta, ContaGrafica contaMaster, decimal valorTotalInvestimento,
        CancellationToken cancellationToken)
    {
        var tickersCesta = cesta.Itens.Select(i => TickerUtils.Normalize(i.Ticker)).Distinct().ToList();
        var saldoMasterPorTicker = (await custodiaRepository.FindByContaGraficaIdsAndTickers(
                [contaMaster.Id], tickersCesta, cancellationToken))
            .ToDictionary(c => TickerUtils.Normalize(c.Ticker), c => c.Quantidade);

        var itensCompra = new List<ItemCompraInfo>();

        foreach (var itemCesta in cesta.Itens)
        {
            var cotacao = cotacaoService.ObterCotacao(itemCesta.Ticker)
                ?? throw new BusinessException(
                    $"Cotacao nao encontrada para o ticker {itemCesta.Ticker}.",
                    ErrorCodes.CotacaoNaoEncontrada);

            // RN-028: Quantidade calculada com truncamento (inteiro para baixo).
            var valorParaTicker = valorTotalInvestimento * (itemCesta.Percentual / TradingConstants.PercentualBase);
            var quantidadeNecessaria = (int)(valorParaTicker / cotacao.PrecoFechamento);

            // RN-029/RN-030/RN-040: Considerar saldo master e descontar da nova compra.
            var saldoMaster = saldoMasterPorTicker.GetValueOrDefault(TickerUtils.Normalize(itemCesta.Ticker), 0);

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
                    Ticker = item.Ticker + TradingConstants.SufixoTickerFracionario,
                    Quantidade = item.QuantidadeFracionario,
                    PrecoUnitario = item.PrecoUnitario,
                    ValorTotal = item.QuantidadeFracionario * item.PrecoUnitario,
                    TipoMercado = TipoMercado.FRACIONARIO
                });
            }
        }

        return ordem;
    }

    private void ZerarSaldoMasterConsumido(
        List<ItemCompraInfo> itensCompra,
        long contaMasterId,
        Dictionary<(long ContaGraficaId, string Ticker), Custodia> custodiasCache)
    {
        foreach (var item in itensCompra)
        {
            if (item.SaldoMasterAnterior > 0)
            {
                if (custodiasCache.TryGetValue((contaMasterId, TickerUtils.Normalize(item.Ticker)), out var custodiaMaster))
                    custodiaMaster.Quantidade = 0;
            }
        }
    }

    private (Distribuicao, List<IrDedoDuroEvent>) DistribuirParaClientes(
        OrdemCompra ordem, List<ItemCompraInfo> itensCompra, List<Cliente> clientes,
        ContaGrafica contaMaster, decimal valorTotalInvestimento,
        Dictionary<(long ContaGraficaId, string Ticker), Custodia> custodiasCache)
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
                AtualizarCustodiaComCache(
                    cliente.ContaGrafica.Id, item.Ticker, quantidadeCliente,
                    item.PrecoUnitario, custodiasCache);

                // RN-053/RN-054/RN-056: Calcular IR dedo-duro por operacao distribuida.
                eventosIr.Add(irCalculationService.CalcularIrDedoDuro(
                    cliente.Id, cliente.Cpf, item.Ticker, TradingConstants.TipoOperacaoCompra,
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

                AtualizarCustodiaComCache(
                    contaMaster.Id, item.Ticker, residuo,
                    item.PrecoUnitario, custodiasCache);
            }
        }

        return (distribuicao, eventosIr);
    }

    private void AtualizarCustodiaComCache(
        long contaGraficaId,
        string ticker,
        int quantidadeNova,
        decimal precoCompra,
        Dictionary<(long ContaGraficaId, string Ticker), Custodia> custodiasCache)
    {
        var normalizedTicker = TickerUtils.Normalize(ticker);
        var key = (contaGraficaId, normalizedTicker);

        if (custodiasCache.TryGetValue(key, out var custodia))
        {
            custodia.PrecoMedio =
                (custodia.Quantidade * custodia.PrecoMedio + quantidadeNova * precoCompra)
                / (custodia.Quantidade + quantidadeNova);
            custodia.Quantidade += quantidadeNova;
            custodia.DataUltimaAtualizacao = dateTimeProvider.UtcNow;
            return;
        }

        var novaCustodia = new Custodia
        {
            Ticker = normalizedTicker,
            Quantidade = quantidadeNova,
            PrecoMedio = precoCompra,
            ContaGraficaId = contaGraficaId,
            DataUltimaAtualizacao = dateTimeProvider.UtcNow
        };

        custodiaRepository.Add(novaCustodia);
        custodiasCache[key] = novaCustodia;
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
