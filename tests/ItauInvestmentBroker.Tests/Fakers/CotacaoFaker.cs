using Bogus;
using ItauInvestmentBroker.Application.Common.Models;

namespace ItauInvestmentBroker.Tests.Fakers;

public static class CotacaoFaker
{
    public static Cotacao Criar(string ticker, decimal precoFechamento) => new()
    {
        Ticker = ticker,
        PrecoFechamento = precoFechamento,
        PrecoAbertura = precoFechamento * 0.98m,
        PrecoMaximo = precoFechamento * 1.02m,
        PrecoMinimo = precoFechamento * 0.97m,
        PrecoMedio = precoFechamento,
        DataPregao = DateTime.UtcNow.Date,
        QuantidadeNegociada = new Faker().Random.Long(100000, 5000000),
        VolumeNegociado = new Faker().Finance.Amount(1000000, 50000000)
    };
}
