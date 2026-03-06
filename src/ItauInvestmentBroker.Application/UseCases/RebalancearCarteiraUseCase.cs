using ItauInvestmentBroker.Application.Configuration;
using ItauInvestmentBroker.Application.DTOs.Motor;
using ItauInvestmentBroker.Application.Interfaces;
using ItauInvestmentBroker.Application.Services;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace ItauInvestmentBroker.Application.UseCases;

public class RebalancearCarteiraUseCase(
    IClienteRepository clienteRepository,
    ICustodiaRepository custodiaRepository,
    ICotacaoService cotacaoService,
    CustodiaAppService custodiaAppService,
    IrCalculationService irCalculationService,
    KafkaEventPublisher kafkaEventPublisher,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork,
    IOptions<MotorSettings> motorSettings)
{
    private readonly MotorSettings _settings = motorSettings.Value;

    public async Task Executar(Cesta cestaAntiga, Cesta cestaNova, CancellationToken cancellationToken = default)
    {
        // RN-045/RN-046: Rebalanceamento por mudanca de cesta para clientes ativos.
        var clientes = (await clienteRepository.FindAtivos(cancellationToken)).ToList();
        if (clientes.Count == 0)
            return;

        var tickersAntigos = cestaAntiga.Itens.Select(i => i.Ticker).ToHashSet();
        var tickersNovos = cestaNova.Itens.ToDictionary(i => i.Ticker, i => i.Percentual);

        var tickersQueSairam = tickersAntigos.Where(t => !tickersNovos.ContainsKey(t)).ToList();
        var tickersQueEntraram = tickersNovos.Keys.Where(t => !tickersAntigos.Contains(t)).ToList();

        var eventosIrDedoDuro = new List<IrDedoDuroEvent>();
        var eventosIrVenda = new List<IrVendaEvent>();

        foreach (var cliente in clientes)
        {
            var contaGrafica = cliente.ContaGrafica;
            if (contaGrafica is null)
                continue;

            var vendasCliente = new List<VendaInfo>();
            decimal valorObtidoVendas = 0;

            // RN-047: Vender toda a posicao dos ativos que sairam da cesta.
            foreach (var ticker in tickersQueSairam)
            {
                var resultado = await custodiaAppService.VenderPosicao(
                    contaGrafica.Id, ticker, cotacaoService, cancellationToken);
                if (resultado is null)
                    continue;

                vendasCliente.Add(resultado);
                valorObtidoVendas += resultado.ValorVenda;
            }

            // RN-049: Rebalancear ativos que mudaram de percentual.
            var custodias = (await custodiaRepository.FindByContaGraficaId(contaGrafica.Id, cancellationToken))
                .Where(c => c.Quantidade > 0)
                .ToList();

            var valorTotalCarteira = custodias.Sum(c =>
            {
                var cot = cotacaoService.ObterCotacao(c.Ticker);
                return c.Quantidade * (cot?.PrecoFechamento ?? 0);
            }) + valorObtidoVendas;

            var tickersComDeficit = new List<(string Ticker, decimal Deficit, decimal Preco)>();

            foreach (var itemNovo in cestaNova.Itens)
            {
                if (tickersQueEntraram.Contains(itemNovo.Ticker))
                    continue;

                var custodia = custodias.FirstOrDefault(c => c.Ticker == itemNovo.Ticker);
                if (custodia is null || custodia.Quantidade == 0)
                    continue;

                var cotacao = cotacaoService.ObterCotacao(itemNovo.Ticker);
                if (cotacao is null)
                    continue;

                var valorAtual = custodia.Quantidade * cotacao.PrecoFechamento;
                var valorAlvo = valorTotalCarteira * (itemNovo.Percentual / 100m);
                var diferenca = valorAtual - valorAlvo;

                if (diferenca > cotacao.PrecoFechamento)
                {
                    var quantidadeVender = (int)(diferenca / cotacao.PrecoFechamento);
                    var valorVenda = quantidadeVender * cotacao.PrecoFechamento;
                    var lucro = quantidadeVender * (cotacao.PrecoFechamento - custodia.PrecoMedio);

                    vendasCliente.Add(new VendaInfo(itemNovo.Ticker, quantidadeVender, cotacao.PrecoFechamento, custodia.PrecoMedio, lucro, valorVenda));
                    valorObtidoVendas += valorVenda;

                    // RN-043: Venda nao altera PM
                    custodia.Quantidade -= quantidadeVender;
                    custodia.DataUltimaAtualizacao = dateTimeProvider.UtcNow;
                }
                else if (diferenca < -cotacao.PrecoFechamento)
                {
                    tickersComDeficit.Add((itemNovo.Ticker, Math.Abs(diferenca), cotacao.PrecoFechamento));
                }
            }

            // RN-048: Com o valor obtido nas vendas, comprar novos ativos e ativos com deficit.
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
            if (eventoIrVenda is not null)
                eventosIrVenda.Add(eventoIrVenda);
        }

        // RN-055/RN-062: Persistir antes da publicacao de eventos fiscais.
        await unitOfWork.CommitAsync(cancellationToken);

        await kafkaEventPublisher.PublicarEventosIr(eventosIrDedoDuro, eventosIrVenda);
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
            var pesoDeficit = deficit / valorTotalCarteira * 100m;
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
                cliente.Id, cliente.Cpf, ticker, "COMPRA",
                quantidade, preco, dateTimeProvider.UtcNow));
        }

        return eventos;
    }
}
