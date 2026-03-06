using Bogus;
using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Motor.Entities;

namespace ItauInvestmentBroker.Tests.Fakers;

public static class CestaFaker
{
    private static readonly string[] TickersPool = ["PETR4", "VALE3", "ITUB4", "BBDC4", "ABEV3", "WEGE3", "MGLU3", "RENT3", "B3SA3", "ELET3"];

    public static Faker<Cesta> Criar() => new Faker<Cesta>("pt_BR")
        .RuleFor(c => c.Id, f => f.IndexFaker + 1)
        .RuleFor(c => c.Nome, f => f.Commerce.ProductName())
        .RuleFor(c => c.Ativa, true)
        .RuleFor(c => c.DataCriacao, f => f.Date.Past())
        .RuleFor(c => c.Itens, f =>
        {
            var tickers = f.PickRandom(TickersPool, 5).ToList();
            var percentuais = GerarPercentuais(5);
            return tickers.Select((t, i) => new ItemCesta
            {
                Ticker = t,
                Percentual = percentuais[i]
            }).ToList<ItemCesta>();
        });

    public static Cesta CriarComTickers(params string[] tickers)
    {
        var percentual = 100m / tickers.Length;
        var resto = 100m - percentual * tickers.Length;

        return new Cesta
        {
            Id = new Faker().Random.Long(1, 1000),
            Nome = new Faker("pt_BR").Commerce.ProductName(),
            Ativa = true,
            DataCriacao = DateTime.UtcNow,
            Itens = tickers.Select((t, i) => new ItemCesta
            {
                Ticker = t,
                Percentual = i == 0 ? percentual + resto : percentual
            }).ToList()
        };
    }

    private static List<decimal> GerarPercentuais(int count)
    {
        var result = new List<decimal>();
        var restante = 100m;
        for (var i = 0; i < count - 1; i++)
        {
            var valor = Math.Round(restante / (count - i), 0);
            result.Add(valor);
            restante -= valor;
        }
        result.Add(restante);
        return result;
    }
}
