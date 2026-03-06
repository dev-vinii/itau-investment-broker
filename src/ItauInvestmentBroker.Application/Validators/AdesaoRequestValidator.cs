using FluentValidation;
using ItauInvestmentBroker.Application.DTOs.Cliente;

namespace ItauInvestmentBroker.Application.Validators;

public class AdesaoRequestValidator : AbstractValidator<AdesaoRequest>
{
    public AdesaoRequestValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome é obrigatório.");

        RuleFor(x => x.Cpf)
            .NotEmpty().WithMessage("O CPF é obrigatório.")
            .Length(11).WithMessage("O CPF deve conter 11 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .EmailAddress().WithMessage("O e-mail informado é inválido.");

        RuleFor(x => x.ValorMensal)
            .GreaterThanOrEqualTo(100)
            .WithMessage("O valor mensal mínimo é R$100.");
    }
}
