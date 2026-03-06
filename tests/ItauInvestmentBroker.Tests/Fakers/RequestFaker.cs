using Bogus;
using Bogus.Extensions.Brazil;
using ItauInvestmentBroker.Application.Features.Cestas.DTOs;
using ItauInvestmentBroker.Application.Features.Clientes.DTOs;

namespace ItauInvestmentBroker.Tests.Fakers;

public static class RequestFaker
{
    public static Faker<AdesaoRequest> AdesaoRequest() => new Faker<AdesaoRequest>("pt_BR")
        .CustomInstantiator(f => new AdesaoRequest(
            f.Person.FullName,
            f.Person.Cpf(false),
            f.Internet.Email(),
            f.Finance.Amount(100, 5000)));

    public static Faker<ValorMensalRequest> ValorMensalRequest() => new Faker<ValorMensalRequest>("pt_BR")
        .CustomInstantiator(f => new ValorMensalRequest(f.Finance.Amount(100, 10000)));

    public static Faker<CestaRequest> CestaRequest() => new Faker<CestaRequest>("pt_BR")
        .CustomInstantiator(f =>
        {
            var tickers = f.PickRandom(
                new[] { "PETR4", "VALE3", "ITUB4", "BBDC4", "ABEV3", "WEGE3", "MGLU3", "RENT3" }, 5).ToList();
            return new CestaRequest(
                f.Commerce.ProductName(),
                new List<ItemCestaRequest>
                {
                    new(tickers[0], 20m),
                    new(tickers[1], 20m),
                    new(tickers[2], 20m),
                    new(tickers[3], 20m),
                    new(tickers[4], 20m)
                });
        });
}
