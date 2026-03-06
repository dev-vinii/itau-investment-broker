using FluentAssertions;
using FluentValidation.TestHelper;
using ItauInvestmentBroker.Application.DTOs.Cliente;
using ItauInvestmentBroker.Application.Validators;

namespace ItauInvestmentBroker.Tests.Validators;

public class AdesaoRequestValidatorTests
{
    private readonly AdesaoRequestValidator _validator = new();

    [Fact]
    public void Deve_Passar_Com_Dados_Validos()
    {
        var request = new AdesaoRequest("João Silva", "12345678901", "joao@email.com", 100m);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Deve_Falhar_Quando_Nome_Vazio(string? nome)
    {
        var request = new AdesaoRequest(nome!, "12345678901", "joao@email.com", 100m);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Nome);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Deve_Falhar_Quando_Cpf_Vazio(string? cpf)
    {
        var request = new AdesaoRequest("João", cpf!, "joao@email.com", 100m);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Cpf);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789012")]
    public void Deve_Falhar_Quando_Cpf_Tamanho_Invalido(string cpf)
    {
        var request = new AdesaoRequest("João", cpf, "joao@email.com", 100m);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Cpf);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Deve_Falhar_Quando_Email_Vazio(string? email)
    {
        var request = new AdesaoRequest("João", "12345678901", email!, 100m);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Deve_Falhar_Quando_Email_Invalido()
    {
        var request = new AdesaoRequest("João", "12345678901", "email-invalido", 100m);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(99.99)]
    public void Deve_Falhar_Quando_ValorMensal_Abaixo_Minimo(decimal valor)
    {
        var request = new AdesaoRequest("João", "12345678901", "joao@email.com", valor);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ValorMensal);
    }

    [Fact]
    public void Deve_Aceitar_ValorMensal_Exatamente_100()
    {
        var request = new AdesaoRequest("João", "12345678901", "joao@email.com", 100m);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.ValorMensal);
    }
}
