using FluentValidation;
using ItauInvestmentBroker.Application.Common.Constants;
using ItauInvestmentBroker.Application.Features.Clientes.DTOs;

namespace ItauInvestmentBroker.Application.Features.Clientes.Validators;

public class AdesaoRequestValidator : AbstractValidator<AdesaoRequest>
{
    public AdesaoRequestValidator()
    {
        // RN-001: Nome e obrigatorio na adesao.
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome é obrigatório.");

        // RN-001: CPF e obrigatorio na adesao.
        RuleFor(x => x.Cpf)
            .NotEmpty().WithMessage("O CPF é obrigatório.")
            .Length(BusinessConstants.CpfLength).WithMessage($"O CPF deve conter {BusinessConstants.CpfLength} caracteres.");

        // RN-001: Email e obrigatorio na adesao.
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .EmailAddress().WithMessage("O e-mail informado é inválido.");

        // RN-003: Valor mensal minimo de adesao e R$100.
        RuleFor(x => x.ValorMensal)
            .GreaterThanOrEqualTo(BusinessConstants.ValorMensalMinimo)
            .WithMessage($"O valor mensal mínimo é R${BusinessConstants.ValorMensalMinimo:0}.");
    }
}
