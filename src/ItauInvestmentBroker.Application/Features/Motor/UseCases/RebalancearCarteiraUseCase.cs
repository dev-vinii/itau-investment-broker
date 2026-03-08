using ItauInvestmentBroker.Application.Common.Configuration;
using ItauInvestmentBroker.Application.Common.Constants;
using ItauInvestmentBroker.Application.Features.Motor.DTOs;
using ItauInvestmentBroker.Application.Common.Interfaces;
using ItauInvestmentBroker.Application.Services;
using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Motor.Entities;
using ItauInvestmentBroker.Domain.Cestas.Repositories;
using ItauInvestmentBroker.Domain.Clientes.Repositories;
using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Domain.Motor.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ItauInvestmentBroker.Application.Features.Motor.UseCases;

public class RebalancearCarteiraUseCase(
    IClienteRepository clienteRepository,
    ICustodiaRepository custodiaRepository,
    ICotacaoService cotacaoService,
    CustodiaAppService custodiaAppService,
    IrCalculationService irCalculationService,
    KafkaEventPublisher kafkaEventPublisher,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork,
    ILogger<RebalancearCarteiraUseCase> logger,
    IOptions<MotorSettings> motorSettings)
{
    private readonly MotorSettings _settings = motorSettings.Value;

    public async Task Executar(Cesta cestaAntiga, Cesta cestaNova, CancellationToken cancellationToken = default)
    {
        var tickersAntigos = cestaAntiga.Itens.Select(i => i.Ticker).ToHashSet();
        var tickersNovos = cestaNova.Itens.ToDictionary(i => i.Ticker, i => i.Percentual);

        var tickersQueSairam = tickersAntigos.Where(t => !tickersNovos.ContainsKey(t)).ToList();
        var tickersQueEntraram = tickersNovos.Keys.Where(t => !tickersAntigos.Contains(t)).ToList();

        var totalClientes = await clienteRepository.CountAtivos(cancellationToken);
        if (totalClientes == 0)
            return;

        var tamanhoLote = _settings.TamanhoLoteRebalanceamento;

        for (var skip = 0; skip < totalClientes; skip += tamanhoLote)
        {
            var lote = await clienteRepository.FindAtivosPaginado(skip, tamanhoLote, cancellationToken);
            if (lote.Count == 0)
                break;

            // Bulk load: carregar todas as custodiass do lote de uma vez
            var contaGraficaIds = lote
                .Where(c => c.ContaGrafica is not null)
                .Select(c => c.ContaGrafica!.Id)
                .ToList();

            var todosTickersRelevantes = tickersAntigos.Union(tickersNovos.Keys).ToList();
            var todasCustodias = (await custodiaRepository.FindByContaGraficaIdsAndTickers(
                contaGraficaIds, todosTickersRelevantes, cancellationToken)).ToList();

            var custodiasPorConta = todasCustodias
                .GroupBy(c => c.ContaGraficaId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var eventosIrDedoDuro = new List<IrDedoDuroEvent>();
            var eventosIrVenda = new List<IrVendaEvent>();

            foreach (var cliente in lote)
            {
                try
                {
                    var contaGrafica = cliente.ContaGrafica;
                    if (contaGrafica is null)
                        continue;

                    custodiasPorConta.TryGetValue(contaGrafica.Id, out var custodiasCliente);
                    custodiasCliente ??= [];

                    var (dedoDuro, venda) = await ProcessarCliente(
                        cliente, contaGrafica, custodiasCliente,
                        tickersQueSairam, tickersQueEntraram, tickersNovos, cancellationToken);

                    eventosIrDedoDuro.AddRange(dedoDuro);
                    if (venda is not null)
                        eventosIrVenda.Add(venda);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Erro ao rebalancear carteira do cliente {ClienteId}. Continuando com os demais",
                        cliente.Id);
                }
            }

            await unitOfWork.CommitAsync(cancellationToken);
            await kafkaEventPublisher.PublicarEventosIr(eventosIrDedoDuro, eventosIrVenda);
        }
    }

    private async Task<(List<IrDedoDuroEvent> EventosDedoDuro, IrVendaEvent? EventoVenda)> ProcessarCliente(
        Cliente cliente, ContaGrafica contaGrafica, List<Custodia> custodiasCliente,
        List<string> tickersQueSairam, List<string> tickersQueEntraram,
        Dictionary<string, decimal> tickersNovos, CancellationToken cancellationToken)
    {
        var vendasCliente = new List<VendaInfo>();
        var custodiaPorTicker = custodiasCliente
            .GroupBy(c => c.Ticker, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        decimal valorObtidoVendas = 0;

        // RN-047: Vender toda a posicao dos ativos que sairam da cesta.
        foreach (var ticker in tickersQueSairam)
        {
            if (!custodiaPorTicker.TryGetValue(ticker, out var custodia) || custodia.Quantidade == 0)
                continue;

            var cotacao = cotacaoService.ObterCotacao(ticker);
            if (cotacao is null)
                continue;

            var valorVenda = custodia.Quantidade * cotacao.PrecoFechamento;
            var lucro = custodia.Quantidade * (cotacao.PrecoFechamento - custodia.PrecoMedio);

            vendasCliente.Add(new VendaInfo(ticker, custodia.Quantidade, cotacao.PrecoFechamento, custodia.PrecoMedio, lucro, valorVenda));
            valorObtidoVendas += valorVenda;

            // RN-043: Venda nao altera PM, apenas zera quantidade.
            custodia.Quantidade = 0;
            custodia.DataUltimaAtualizacao = dateTimeProvider.UtcNow;
        }

        // RN-049: Rebalancear ativos que mudaram de percentual.
        var custodiasComQuantidade = custodiasCliente.Where(c => c.Quantidade > 0).ToList();

        var valorTotalCarteira = custodiasComQuantidade.Sum(c =>
        {
            var cot = cotacaoService.ObterCotacao(c.Ticker);
            return c.Quantidade * (cot?.PrecoFechamento ?? 0);
        }) + valorObtidoVendas;

        var tickersComDeficit = new List<(string Ticker, decimal Deficit, decimal Preco)>();

        foreach (var itemNovo in tickersNovos)
        {
            if (tickersQueEntraram.Contains(itemNovo.Key))
                continue;

            if (!custodiaPorTicker.TryGetValue(itemNovo.Key, out var custodia) || custodia.Quantidade == 0)
                continue;

            var cotacao = cotacaoService.ObterCotacao(itemNovo.Key);
            if (cotacao is null)
                continue;

            var valorAtual = custodia.Quantidade * cotacao.PrecoFechamento;
            var valorAlvo = valorTotalCarteira * (itemNovo.Value / TradingConstants.PercentualBase);
            var diferenca = valorAtual - valorAlvo;

            if (diferenca > cotacao.PrecoFechamento)
            {
                var quantidadeVender = (int)(diferenca / cotacao.PrecoFechamento);
                var valorVenda = quantidadeVender * cotacao.PrecoFechamento;
                var lucro = quantidadeVender * (cotacao.PrecoFechamento - custodia.PrecoMedio);

                vendasCliente.Add(new VendaInfo(itemNovo.Key, quantidadeVender, cotacao.PrecoFechamento, custodia.PrecoMedio, lucro, valorVenda));
                valorObtidoVendas += valorVenda;

                // RN-043: Venda nao altera PM
                custodia.Quantidade -= quantidadeVender;
                custodia.DataUltimaAtualizacao = dateTimeProvider.UtcNow;
            }
            else if (diferenca < -cotacao.PrecoFechamento)
            {
                tickersComDeficit.Add((itemNovo.Key, Math.Abs(diferenca), cotacao.PrecoFechamento));
            }
        }

        // RN-048: Com o valor obtido nas vendas, comprar novos ativos e ativos com deficit.
        var eventosIrDedoDuro = new List<IrDedoDuroEvent>();
        if (valorObtidoVendas > 0)
        {
            var comprasRealizadas = await ComprarAtivosProporcionalmente(
                contaGrafica.Id, cliente, tickersQueEntraram, tickersNovos,
                tickersComDeficit, valorObtidoVendas, valorTotalCarteira, cancellationToken);
            eventosIrDedoDuro.AddRange(comprasRealizadas);
        }

        // RN-057: Persistir vendas e calcular IR.
        irCalculationService.PersistirVendas(cliente.Id, vendasCliente);

        var eventoIrVenda = await irCalculationService.CalcularIrVenda(cliente, vendasCliente, cancellationToken);

        return (eventosIrDedoDuro, eventoIrVenda);
    }

    private async Task<List<IrDedoDuroEvent>> ComprarAtivosProporcionalmente(
        long contaGraficaId, Cliente cliente,
        List<string> tickersQueEntraram, Dictionary<string, decimal> tickersNovos,
        List<(string Ticker, decimal Deficit, decimal Preco)> tickersComDeficit,
        decimal valorObtidoVendas, decimal valorTotalCarteira,
        CancellationToken cancellationToken)
    {
        var eventos = new List<IrDedoDuroEvent>();
        var tickersParaComprar = new List<(string Ticker, decimal Peso, decimal Preco)>();

        foreach (var ticker in tickersQueEntraram)
            tickersParaComprar.Add((ticker, tickersNovos[ticker], 0));

        // RN-049: Ativos remanescentes com deficit
        foreach (var (ticker, deficit, preco) in tickersComDeficit)
        {
            var pesoDeficit = deficit / valorTotalCarteira * TradingConstants.PercentualBase;
            tickersParaComprar.Add((ticker, pesoDeficit, preco));
        }

        if (tickersParaComprar.Count == 0)
            return eventos;

        var somaPesos = tickersParaComprar.Sum(t => t.Peso);

        foreach (var (ticker, peso, precoCache) in tickersParaComprar)
        {
            var cotacao = cotacaoService.ObterCotacao(ticker);
            var preco = cotacao?.PrecoFechamento ?? precoCache;
            if (preco == 0) continue;

            var proporcao = peso / somaPesos;
            var valorParaTicker = valorObtidoVendas * proporcao;
            var quantidade = (int)(valorParaTicker / preco);

            if (quantidade == 0)
                continue;

            await custodiaAppService.AtualizarCustodia(
                contaGraficaId, ticker, quantidade, preco, cancellationToken);

            eventos.Add(irCalculationService.CalcularIrDedoDuro(
                cliente.Id, cliente.Cpf, ticker, TradingConstants.TipoOperacaoCompra,
                quantidade, preco, dateTimeProvider.UtcNow));
        }

        return eventos;
    }
}
