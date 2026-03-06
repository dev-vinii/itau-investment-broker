using FluentValidation;
using ItauInvestmentBroker.Application.DTOs.Cliente;

namespace ItauInvestmentBroker.Application.Validators;

public class ValorMensalRequestValidator : AbstractValidator<ValorMensalRequest>
{
    public ValorMensalRequestValidator()
    {
        // RN-003/RN-011: Alteracao de valor mensal respeita o minimo de R$100.
        RuleFor(x => x.ValorMensal)
            .GreaterThanOrEqualTo(100)
            .WithMessage("O valor mensal mínimo é R$100.");
    }
}
