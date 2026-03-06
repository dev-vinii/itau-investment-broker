using Bogus;
using Bogus.Extensions.Brazil;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Enums;

namespace ItauInvestmentBroker.Tests.Fakers;

public static class ClienteFaker
{
    private static readonly string[] Tickers = ["PETR4", "VALE3", "ITUB4", "BBDC4", "ABEV3", "WEGE3", "MGLU3", "RENT3"];

    public static Faker<Cliente> Criar() => new Faker<Cliente>("pt_BR")
        .RuleFor(c => c.Id, f => f.IndexFaker + 1)
        .RuleFor(c => c.Nome, f => f.Person.FullName)
        .RuleFor(c => c.Cpf, f => f.Person.Cpf(false))
        .RuleFor(c => c.Email, f => f.Internet.Email())
        .RuleFor(c => c.ValorMensal, f => f.Finance.Amount(100, 5000))
        .RuleFor(c => c.Ativo, true)
        .RuleFor(c => c.DataAdesao, f => f.Date.Past());

    public static Faker<Cliente> CriarComConta() => Criar()
        .RuleFor(c => c.ContaGrafica, (f, c) => Fakers.ContaGraficaFaker.CriarFilhote().Generate());
}

public static class ContaGraficaFaker
{
    public static Faker<ContaGrafica> CriarFilhote() => new Faker<ContaGrafica>("pt_BR")
        .RuleFor(c => c.Id, f => f.IndexFaker + 100)
        .RuleFor(c => c.NumeroConta, f => f.Random.AlphaNumeric(10).ToUpper())
        .RuleFor(c => c.Tipo, TipoConta.FILHOTE);

    public static Faker<ContaGrafica> CriarMaster() => new Faker<ContaGrafica>("pt_BR")
        .RuleFor(c => c.Id, f => f.IndexFaker + 1000)
        .RuleFor(c => c.NumeroConta, f => "MASTER" + f.Random.AlphaNumeric(4).ToUpper())
        .RuleFor(c => c.Tipo, TipoConta.MASTER);
}
