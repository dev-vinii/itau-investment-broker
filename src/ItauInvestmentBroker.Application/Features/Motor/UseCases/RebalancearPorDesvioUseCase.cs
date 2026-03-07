using ItauInvestmentBroker.Application.Common.Configuration;
using ItauInvestmentBroker.Application.Common.Constants;
using ItauInvestmentBroker.Application.Features.Motor.DTOs;
using ItauInvestmentBroker.Application.Common.Exceptions;
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

public class RebalancearPorDesvioUseCase(
    ICestaRepository cestaRepository,
    IClienteRepository clienteRepository,
    ICustodiaRepository custodiaRepository,
    ICotacaoService cotacaoService,
    CustodiaAppService custodiaAppService,
    IrCalculationService irCalculationService,
    KafkaEventPublisher kafkaEventPublisher,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork,
    ILogger<RebalancearPorDesvioUseCase> logger,
    IOptions<MotorSettings> motorSettings)
{
    private readonly MotorSettings _settings = motorSettings.Value;

    public async Task<RebalancearPorDesvioResponse> Executar(CancellationToken cancellationToken = default)
    {
        // RN-050: Rebalanceamento por desvio usa composicao da cesta ativa.
        var cesta = await cestaRepository.FindAtiva(cancellationToken)
            ?? throw new NotFoundException("Nenhuma cesta ativa encontrada.", ErrorCodes.CestaNaoEncontrada);

        // RN-024: Apenas clientes ativos participam do rebalanceamento.
        var totalClientes = await clienteRepository.CountAtivos(cancellationToken);
        if (totalClientes == 0)
            throw new BusinessException("Nenhum cliente ativo encontrado.", ErrorCodes.ClienteNaoEncontrado);

        var percentuaisAlvo = cesta.Itens.ToDictionary(i => i.Ticker, i => i.Percentual);
        var tickersCesta = percentuaisAlvo.Keys.ToList();
        var clientesRebalanceados = 0;
        var tamanhoLote = _settings.TamanhoLoteRebalanceamento;

        for (var skip = 0; skip < totalClientes; skip += tamanhoLote)
        {
            var lote = await clienteRepository.FindAtivosPaginado(skip, tamanhoLote, cancellationToken);
            if (lote.Count == 0)
                break;

            // Bulk load: carregar todas as custodias do lote de uma vez
            var contaGraficaIds = lote
                .Where(c => c.ContaGrafica is not null)
                .Select(c => c.ContaGrafica!.Id)
                .ToList();

            var todasCustodias = (await custodiaRepository.FindByContaGraficaIdsAndTickers(
                contaGraficaIds, tickersCesta, cancellationToken)).ToList();

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

                    var resultado = await RebalancearCliente(
                        cliente, contaGrafica, custodiasCliente, percentuaisAlvo, cancellationToken);

                    if (resultado is null)
                        continue;

                    clientesRebalanceados++;
                    eventosIrDedoDuro.AddRange(resultado.Value.EventosDedoDuro);
                    if (resultado.Value.EventoVenda is not null)
                        eventosIrVenda.Add(resultado.Value.EventoVenda);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Erro ao rebalancear por desvio do cliente {ClienteId}. Continuando com os demais",
                        cliente.Id);
                }
            }

            // RN-055/RN-062: Persistir antes da publicacao.
            await unitOfWork.CommitAsync(cancellationToken);
            await kafkaEventPublisher.PublicarEventosIr(eventosIrDedoDuro, eventosIrVenda);
        }

        return new RebalancearPorDesvioResponse(clientesRebalanceados, _settings.LimiarDesvioPontos);
    }

    private async Task<(List<IrDedoDuroEvent> EventosDedoDuro, IrVendaEvent? EventoVenda)?> RebalancearCliente(
        Cliente cliente, ContaGrafica contaGrafica,
        List<Custodia> custodiasCliente,
        Dictionary<string, decimal> percentuaisAlvo, CancellationToken cancellationToken)
    {
        var custodias = custodiasCliente.Where(c => c.Quantidade > 0).ToList();

        if (custodias.Count == 0)
            return null;

        var valorTotalCarteira = custodias.Sum(c =>
        {
            var cot = cotacaoService.ObterCotacao(c.Ticker);
            return c.Quantidade * (cot?.PrecoFechamento ?? 0);
        });

        if (valorTotalCarteira == 0)
            return null;

        // RN-051: Verificar desvio por ativo com limiar configurado.
        var desvios = CalcularDesvios(custodias, percentuaisAlvo, valorTotalCarteira);

        if (desvios.Count == 0)
            return null;

        var eventosIrDedoDuro = new List<IrDedoDuroEvent>();
        var vendasCliente = new List<VendaInfo>();
        decimal valorDisponivel = 0;

        // RN-052: Vender ativos sobre-alocados.
        foreach (var desvio in desvios.Where(d => d.Desvio > 0))
        {
            var valorAlvo = valorTotalCarteira * (desvio.PercentualAlvo / TradingConstants.PercentualBase);
            var valorAtual = desvio.Custodia.Quantidade * desvio.PrecoAtual;
            var excesso = valorAtual - valorAlvo;

            var quantidadeVender = (int)(excesso / desvio.PrecoAtual);
            if (quantidadeVender == 0)
                continue;

            var valorVenda = quantidadeVender * desvio.PrecoAtual;
            var lucro = quantidadeVender * (desvio.PrecoAtual - desvio.Custodia.PrecoMedio);

            vendasCliente.Add(new VendaInfo(desvio.Ticker, quantidadeVender, desvio.PrecoAtual, desvio.Custodia.PrecoMedio, lucro, valorVenda));
            valorDisponivel += valorVenda;

            desvio.Custodia.Quantidade -= quantidadeVender;
            desvio.Custodia.DataUltimaAtualizacao = dateTimeProvider.UtcNow;
        }

        // Comprar ativos sub-alocados com o valor obtido.
        if (valorDisponivel > 0)
        {
            var subAlocados = desvios.Where(d => d.Desvio < 0).ToList();
            var somaDeficits = subAlocados.Sum(d => Math.Abs(d.Desvio));

            if (somaDeficits > 0)
            {
                foreach (var desvio in subAlocados)
                {
                    var proporcao = Math.Abs(desvio.Desvio) / somaDeficits;
                    var valorParaTicker = valorDisponivel * proporcao;
                    var quantidade = (int)(valorParaTicker / desvio.PrecoAtual);

                    if (quantidade == 0)
                        continue;

                    await custodiaAppService.AtualizarCustodia(
                        contaGrafica.Id, desvio.Ticker, quantidade,
                        desvio.PrecoAtual, cancellationToken);

                    eventosIrDedoDuro.Add(irCalculationService.CalcularIrDedoDuro(
                        cliente.Id, cliente.Cpf, desvio.Ticker, TradingConstants.TipoOperacaoCompra,
                        quantidade, desvio.PrecoAtual, dateTimeProvider.UtcNow));
                }
            }
        }

        // RN-057: Persistir vendas e calcular IR.
        irCalculationService.PersistirVendas(cliente.Id, vendasCliente);
        var eventoIrVenda = await irCalculationService.CalcularIrVenda(cliente, vendasCliente, cancellationToken);

        return (eventosIrDedoDuro, eventoIrVenda);
    }

    private List<DesvioInfo> CalcularDesvios(
        List<Custodia> custodias,
        Dictionary<string, decimal> percentuaisAlvo,
        decimal valorTotalCarteira)
    {
        var desvios = new List<DesvioInfo>();

        foreach (var custodia in custodias)
        {
            if (!percentuaisAlvo.TryGetValue(custodia.Ticker, out var percentualAlvo))
                continue;

            var cotacao = cotacaoService.ObterCotacao(custodia.Ticker);
            if (cotacao is null)
                continue;

            var valorAtual = custodia.Quantidade * cotacao.PrecoFechamento;
            var percentualReal = valorAtual / valorTotalCarteira * TradingConstants.PercentualBase;
            var desvio = percentualReal - percentualAlvo;

            if (Math.Abs(desvio) >= _settings.LimiarDesvioPontos)
            {
                desvios.Add(new DesvioInfo(
                    custodia.Ticker, percentualReal, percentualAlvo,
                    desvio, cotacao.PrecoFechamento, custodia));
            }
        }

        return desvios;
    }

    private record DesvioInfo(string Ticker, decimal PercentualReal, decimal PercentualAlvo, decimal Desvio, decimal PrecoAtual, Custodia Custodia);
}

public record RebalancearPorDesvioResponse(int ClientesRebalanceados, decimal LimiarDesvio);
