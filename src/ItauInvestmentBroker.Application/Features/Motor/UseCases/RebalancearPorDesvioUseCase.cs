using ItauInvestmentBroker.Application.Common.Configuration;
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
    IOptions<MotorSettings> motorSettings)
{
    private readonly MotorSettings _settings = motorSettings.Value;

    public async Task<RebalancearPorDesvioResponse> Executar(CancellationToken cancellationToken = default)
    {
        // RN-050: Rebalanceamento por desvio usa composicao da cesta ativa.
        var cesta = await cestaRepository.FindAtiva(cancellationToken)
            ?? throw new NotFoundException("Nenhuma cesta ativa encontrada.", ErrorCodes.CestaNaoEncontrada);

        // RN-024: Apenas clientes ativos participam do rebalanceamento.
        var clientes = (await clienteRepository.FindAtivos(cancellationToken)).ToList();
        if (clientes.Count == 0)
            throw new BusinessException("Nenhum cliente ativo encontrado.", ErrorCodes.ClienteNaoEncontrado);

        var percentuaisAlvo = cesta.Itens.ToDictionary(i => i.Ticker, i => i.Percentual);
        var eventosIrDedoDuro = new List<IrDedoDuroEvent>();
        var eventosIrVenda = new List<IrVendaEvent>();
        var clientesRebalanceados = 0;

        foreach (var cliente in clientes)
        {
            var contaGrafica = cliente.ContaGrafica;
            if (contaGrafica is null)
                continue;

            var resultado = await RebalancearCliente(
                cliente, contaGrafica, percentuaisAlvo, cancellationToken);

            if (resultado is null)
                continue;

            clientesRebalanceados++;
            eventosIrDedoDuro.AddRange(resultado.Value.EventosDedoDuro);
            if (resultado.Value.EventoVenda is not null)
                eventosIrVenda.Add(resultado.Value.EventoVenda);
        }

        // RN-055/RN-062: Persistir antes da publicacao.
        await unitOfWork.CommitAsync(cancellationToken);

        await kafkaEventPublisher.PublicarEventosIr(eventosIrDedoDuro, eventosIrVenda);

        return new RebalancearPorDesvioResponse(clientesRebalanceados, _settings.LimiarDesvioPontos);
    }

    private async Task<(List<IrDedoDuroEvent> EventosDedoDuro, IrVendaEvent? EventoVenda)?> RebalancearCliente(
        Cliente cliente, ContaGrafica contaGrafica,
        Dictionary<string, decimal> percentuaisAlvo, CancellationToken cancellationToken)
    {
        var custodias = (await custodiaRepository.FindByContaGraficaId(contaGrafica.Id, cancellationToken))
            .Where(c => c.Quantidade > 0)
            .ToList();

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
            var valorAlvo = valorTotalCarteira * (desvio.PercentualAlvo / 100m);
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
                        cliente.Id, cliente.Cpf, desvio.Ticker, "COMPRA",
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
            var percentualReal = valorAtual / valorTotalCarteira * 100m;
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
