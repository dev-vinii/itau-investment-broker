using FluentValidation;
using ItauInvestmentBroker.Application.Features.Cestas.DTOs;

namespace ItauInvestmentBroker.Application.Features.Cestas.Validators;

public class CestaRequestValidator : AbstractValidator<CestaRequest>
{
    public CestaRequestValidator()
    {
        // RN-014: Cesta deve conter exatamente 5 acoes.
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome da cesta é obrigatório.");

        // RN-014: Cesta deve conter exatamente 5 ativos.
        RuleFor(x => x.Itens)
            .Must(i => i.Count == 5)
            .WithMessage(x => $"A cesta deve conter exatamente 5 ativos. Quantidade informada: {x.Itens.Count}.");

        // RN-015: Soma dos percentuais deve ser 100% (tolerancia para precisao decimal).
        RuleFor(x => x.Itens)
            .Must(i => Math.Abs(i.Sum(item => item.Percentual) - 100m) < 0.01m)
            .WithMessage(x => $"A soma dos percentuais deve ser exatamente 100%. Soma atual: {x.Itens.Sum(i => i.Percentual)}%.");

        // RN-016: Cada percentual deve ser maior que 0%.
        RuleForEach(x => x.Itens)
            .Must(item => item.Percentual > 0)
            .WithMessage("Cada ativo deve ter percentual maior que 0%.");

        // Regra adicional de consistencia: nao permitir ticker duplicado na cesta.
        RuleFor(x => x.Itens)
            .Must(i => i.Select(item => item.Ticker).Distinct().Count() == i.Count)
            .WithMessage("Não são permitidos tickers duplicados na cesta.");
    }
}
