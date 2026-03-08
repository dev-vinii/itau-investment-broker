using ItauInvestmentBroker.Application.Common.Configuration;
using ItauInvestmentBroker.Application.Common.Constants;
using ItauInvestmentBroker.Application.Common.Exceptions;
using ItauInvestmentBroker.Application.Common.Interfaces;
using ItauInvestmentBroker.Application.Common.Utils;
using ItauInvestmentBroker.Application.Features.Motor.DTOs;
using ItauInvestmentBroker.Application.Services;
using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Cestas.Repositories;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Clientes.Repositories;
using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Domain.Motor.Entities;
using ItauInvestmentBroker.Domain.Motor.Enums;
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

    public async Task<ExecutarCompraResponse> Executar(CancellationToken cancellationToken = default)
    {
        // RN-026: Execucao da compra usa a cesta ativa para consolidacao.
        var cesta = await cestaRepository.FindAtiva(cancellationToken)
                    ?? throw new NotFoundException("Nenhuma cesta ativa encontrada.", ErrorCodes.CestaNaoEncontrada);

        // RN-024: Apenas clientes ativos participam da compra programada.
        var totalClientes = await clienteRepository.CountAtivos(cancellationToken);
        if (totalClientes == 0)
            throw new BusinessException("Nenhum cliente ativo encontrado.", ErrorCodes.ClienteNaoEncontrado);

        var contasMaster = (await contaGraficaRepository.FindMaster(cancellationToken)).ToList();
        var contaMaster = contasMaster.FirstOrDefault()
                          ?? throw new NotFoundException("Conta master nao encontrada.",
                              ErrorCodes.ClienteNaoEncontrado);

        // RN-023/RN-025/RN-026: Aporte do ciclo = valor mensal / divisor, consolidado para compra unica.
        var somaValorMensal = await clienteRepository.SomarValorMensalAtivos(cancellationToken);
        var valorTotalInvestimento = somaValorMensal / _settings.DivisorMensal;

        var itensCompra = await CalcularItensCompra(cesta, contaMaster, valorTotalInvestimento, cancellationToken);

        using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var dataExecucao = dateTimeProvider.UtcNow;
            var ordem = CriarOrdemCompra(itensCompra, contaMaster.Id, cesta.Id, dataExecucao);
            ordemCompraRepository.Add(ordem);

            // RN-039: Zerar saldo master consumido.
            var custodiasCache = await CarregarCustodiasMaster(contaMaster, itensCompra, cancellationToken);
            ZerarSaldoMasterConsumido(itensCompra, contaMaster.Id, custodiasCache);

            // RN-034: Distribuicao para filhotes proporcional ao aporte, em lotes.
            var eventosIrTodos = new List<IrDedoDuroEvent>();
            var distribuicoesResponse = new List<DistribuicaoClienteResponse>();
            var tamanhoLote = _settings.TamanhoLoteRebalanceamento;

            for (var skip = 0; skip < totalClientes; skip += tamanhoLote)
            {
                var loteClientes = await clienteRepository.FindAtivosPaginado(skip, tamanhoLote, cancellationToken);
                if (loteClientes.Count == 0)
                    break;

                var contaIdsLote = loteClientes
                    .Where(c => c.ContaGrafica is not null)
                    .Select(c => c.ContaGrafica!.Id)
                    .ToList();

                var tickersCompra = itensCompra.Select(i => i.Ticker).Distinct().ToList();
                var custodiasLote = (await custodiaRepository.FindByContaGraficaIdsAndTickers(
                        contaIdsLote, tickersCompra, cancellationToken))
                    .ToDictionary(c => (c.ContaGraficaId, TickerUtils.Normalize(c.Ticker)), c => c);

                var (distribuicao, eventosIr, distClientes) = DistribuirParaClientes(
                    ordem, itensCompra, loteClientes, contaMaster, valorTotalInvestimento,
                    somaValorMensal, custodiasLote);
                distribuicaoRepository.Add(distribuicao);
                eventosIrTodos.AddRange(eventosIr);
                distribuicoesResponse.AddRange(distClientes);
            }

            // RN-039: Residuo não distribuido permanece em custódia master.
            AdicionarResiduoMaster(ordem, itensCompra, contaMaster, custodiasCache);

            await unitOfWork.CommitTransactionAsync(cancellationToken);

            // RN-055: Publicar eventos de IR apos persistencia.
            await kafkaEventPublisher.PublicarEventosIr(eventosIrTodos, []);

            var ordensCompraResponse = itensCompra.Select(i =>
            {
                var detalhes = new List<DetalheOrdemResponse>();
                if (i.QuantidadeLote > 0)
                    detalhes.Add(new DetalheOrdemResponse("LOTE", i.Ticker, i.QuantidadeLote));
                if (i.QuantidadeFracionario > 0)
                    detalhes.Add(new DetalheOrdemResponse("FRACIONARIO",
                        i.Ticker + TradingConstants.SufixoTickerFracionario, i.QuantidadeFracionario));

                return new OrdemCompraResponse(
                    i.Ticker, i.QuantidadeAComprar, detalhes,
                    i.PrecoUnitario, i.QuantidadeAComprar * i.PrecoUnitario);
            }).ToList();

            var residuosResponse = itensCompra
                .Where(i => i.QuantidadeTotalDisponivel - i.QuantidadeDistribuida > 0)
                .Select(i => new ResiduoMasterResponse(i.Ticker, i.QuantidadeTotalDisponivel - i.QuantidadeDistribuida))
                .ToList();

            return new ExecutarCompraResponse(
                ordem.DataExecucao,
                totalClientes,
                valorTotalInvestimento,
                ordensCompraResponse,
                distribuicoesResponse,
                residuosResponse,
                eventosIrTodos.Count,
                $"Compra programada executada com sucesso para {totalClientes} clientes.");
        }
        catch (Exception)
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task<Dictionary<(long ContaGraficaId, string Ticker), Custodia>> CarregarCustodiasMaster(
        ContaGrafica contaMaster,
        List<ItemCompraInfo> itensCompra,
        CancellationToken cancellationToken)
    {
        var tickers = itensCompra
            .Select(i => i.Ticker)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var custodias = (await custodiaRepository.FindByContaGraficaIdsAndTickers(
                [contaMaster.Id], tickers, cancellationToken))
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
            var quantidadeLote = quantidadeAComprar / _settings.LotePadrao * _settings.LotePadrao;
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
                SaldoMasterAnterior = saldoMaster,
                QuantidadeDistribuida = 0
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
                ordem.Itens.Add(new ItemOrdemCompra
                {
                    Ticker = item.Ticker,
                    Quantidade = item.QuantidadeLote,
                    PrecoUnitario = item.PrecoUnitario,
                    ValorTotal = item.QuantidadeLote * item.PrecoUnitario,
                    TipoMercado = TipoMercado.LOTE
                });

            if (item.QuantidadeFracionario > 0)
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

        return ordem;
    }

    private void ZerarSaldoMasterConsumido(
        List<ItemCompraInfo> itensCompra,
        long contaMasterId,
        Dictionary<(long ContaGraficaId, string Ticker), Custodia> custodiasCache)
    {
        foreach (var item in itensCompra)
            if (item.SaldoMasterAnterior > 0)
                if (custodiasCache.TryGetValue((contaMasterId, TickerUtils.Normalize(item.Ticker)),
                        out var custodiaMaster))
                    custodiaMaster.Quantidade = 0;
    }

    private (Distribuicao, List<IrDedoDuroEvent>, List<DistribuicaoClienteResponse>) DistribuirParaClientes(
        OrdemCompra ordem, List<ItemCompraInfo> itensCompra, List<Cliente> clientes,
        ContaGrafica contaMaster, decimal valorTotalInvestimento, decimal somaValorMensal,
        Dictionary<(long ContaGraficaId, string Ticker), Custodia> custodiasCache)
    {
        var distribuicao = new Distribuicao
        {
            OrdemCompraId = ordem.Id,
            OrdemCompra = ordem
        };

        var eventosIr = new List<IrDedoDuroEvent>();
        var ativosPorCliente = new Dictionary<long, List<AtivoDistribuidoResponse>>();

        foreach (var item in itensCompra)
        foreach (var cliente in clientes)
        {
            // RN-035: Proporcao = aporte do cliente / total de aportes
            var proporcao = cliente.ValorMensal / _settings.DivisorMensal / valorTotalInvestimento;
            // RN-036: Quantidade = TRUNCAR(proporcao x total disponivel)
            var quantidadeCliente = (int)(item.QuantidadeTotalDisponivel * proporcao);

            if (quantidadeCliente == 0)
                continue;

            item.QuantidadeDistribuida += quantidadeCliente;

            distribuicao.Itens.Add(new ItemDistribuicao
            {
                Ticker = item.Ticker,
                Quantidade = quantidadeCliente,
                ContaGraficaId = cliente.ContaGrafica!.Id
            });

            if (!ativosPorCliente.ContainsKey(cliente.Id))
                ativosPorCliente[cliente.Id] = [];
            ativosPorCliente[cliente.Id].Add(new AtivoDistribuidoResponse(item.Ticker, quantidadeCliente));

            // RN-038/RN-041/RN-044: Atualizar custodia e recalcular preco medio.
            AtualizarCustodiaComCache(
                cliente.ContaGrafica.Id, item.Ticker, quantidadeCliente,
                item.PrecoUnitario, custodiasCache);

            // RN-053/RN-054/RN-056: Calcular IR dedo-duro por operacao distribuida.
            eventosIr.Add(irCalculationService.CalcularIrDedoDuro(
                cliente.Id, cliente.Cpf, item.Ticker, TradingConstants.TipoOperacaoCompra,
                quantidadeCliente, item.PrecoUnitario, ordem.DataExecucao));
        }

        var distribuicoesCliente = clientes
            .Where(c => ativosPorCliente.ContainsKey(c.Id))
            .Select(c => new DistribuicaoClienteResponse(
                c.Id, c.Nome,
                c.ValorMensal / _settings.DivisorMensal,
                ativosPorCliente[c.Id]))
            .ToList();

        return (distribuicao, eventosIr, distribuicoesCliente);
    }

    private void AdicionarResiduoMaster(
        OrdemCompra ordem, List<ItemCompraInfo> itensCompra,
        ContaGrafica contaMaster,
        Dictionary<(long ContaGraficaId, string Ticker), Custodia> custodiasCache)
    {
        foreach (var item in itensCompra)
        {
            // RN-039: Residuo nao distribuido permanece em custodia master.
            var residuo = item.QuantidadeTotalDisponivel - item.QuantidadeDistribuida;
            if (residuo <= 0)
                continue;

            AtualizarCustodiaComCache(
                contaMaster.Id, item.Ticker, residuo,
                item.PrecoUnitario, custodiasCache);
        }
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
        public int QuantidadeDistribuida { get; set; }
    }
}