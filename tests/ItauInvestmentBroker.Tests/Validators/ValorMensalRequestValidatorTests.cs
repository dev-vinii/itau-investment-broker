using FluentValidation.TestHelper;
using ItauInvestmentBroker.Application.DTOs.Cliente;
using ItauInvestmentBroker.Application.Validators;

namespace ItauInvestmentBroker.Tests.Validators;

public class ValorMensalRequestValidatorTests
{
    private readonly ValorMensalRequestValidator _validator = new();

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(10000)]
    public void Deve_Passar_Com_Valor_Valido(decimal valor)
    {
        var request = new ValorMensalRequest(valor);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(99.99)]
    [InlineData(-100)]
    public void Deve_Falhar_Com_Valor_Abaixo_Minimo(decimal valor)
    {
        var request = new ValorMensalRequest(valor);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ValorMensal);
    }
}
