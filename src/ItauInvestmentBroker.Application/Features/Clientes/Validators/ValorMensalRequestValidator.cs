using FluentValidation;
using ItauInvestmentBroker.Application.Common.Constants;
using ItauInvestmentBroker.Application.Features.Clientes.DTOs;

namespace ItauInvestmentBroker.Application.Features.Clientes.Validators;

public class ValorMensalRequestValidator : AbstractValidator<ValorMensalRequest>
{
    public ValorMensalRequestValidator()
    {
        // RN-003/RN-011: Alteracao de valor mensal respeita o minimo de R$100.
        RuleFor(x => x.ValorMensal)
            .GreaterThanOrEqualTo(BusinessConstants.ValorMensalMinimo)
            .WithMessage($"O valor mensal mínimo é R${BusinessConstants.ValorMensalMinimo:0}.");
    }
}
