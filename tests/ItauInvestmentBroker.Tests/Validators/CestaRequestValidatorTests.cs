using FluentAssertions;
using FluentValidation.TestHelper;
using ItauInvestmentBroker.Application.Features.Cestas.DTOs;
using ItauInvestmentBroker.Application.Features.Clientes.Validators;
using ItauInvestmentBroker.Application.Features.Cestas.Validators;

namespace ItauInvestmentBroker.Tests.Validators;

public class CestaRequestValidatorTests
{
    private readonly CestaRequestValidator _validator = new();

    private static List<ItemCestaRequest> CriarItensValidos() =>
    [
        new("PETR4", 20m),
        new("VALE3", 20m),
        new("ITUB4", 20m),
        new("BBDC4", 20m),
        new("ABEV3", 20m)
    ];

    [Fact]
    public void Deve_Passar_Com_Dados_Validos()
    {
        var request = new CestaRequest("Cesta Conservadora", CriarItensValidos());
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Deve_Falhar_Quando_Nome_Vazio(string? nome)
    {
        var request = new CestaRequest(nome!, CriarItensValidos());
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Nome);
    }

    [Fact]
    public void Deve_Falhar_Quando_Menos_De_5_Itens()
    {
        var itens = new List<ItemCestaRequest>
        {
            new("PETR4", 25m),
            new("VALE3", 25m),
            new("ITUB4", 25m),
            new("BBDC4", 25m)
        };
        var request = new CestaRequest("Cesta", itens);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Itens);
    }

    [Fact]
    public void Deve_Falhar_Quando_Mais_De_5_Itens()
    {
        var itens = new List<ItemCestaRequest>
        {
            new("PETR4", 16m),
            new("VALE3", 16m),
            new("ITUB4", 17m),
            new("BBDC4", 17m),
            new("ABEV3", 17m),
            new("WEGE3", 17m)
        };
        var request = new CestaRequest("Cesta", itens);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Itens);
    }

    [Fact]
    public void Deve_Falhar_Quando_Soma_Percentuais_Diferente_De_100()
    {
        var itens = new List<ItemCestaRequest>
        {
            new("PETR4", 20m),
            new("VALE3", 20m),
            new("ITUB4", 20m),
            new("BBDC4", 20m),
            new("ABEV3", 10m)
        };
        var request = new CestaRequest("Cesta", itens);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Itens);
    }

    [Fact]
    public void Deve_Falhar_Quando_Percentual_Zero()
    {
        var itens = new List<ItemCestaRequest>
        {
            new("PETR4", 0m),
            new("VALE3", 25m),
            new("ITUB4", 25m),
            new("BBDC4", 25m),
            new("ABEV3", 25m)
        };
        var request = new CestaRequest("Cesta", itens);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Itens[0]");
    }

    [Fact]
    public void Deve_Falhar_Quando_Tickers_Duplicados()
    {
        var itens = new List<ItemCestaRequest>
        {
            new("PETR4", 20m),
            new("PETR4", 20m),
            new("ITUB4", 20m),
            new("BBDC4", 20m),
            new("ABEV3", 20m)
        };
        var request = new CestaRequest("Cesta", itens);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Itens);
    }
}
