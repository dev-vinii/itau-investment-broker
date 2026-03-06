using FluentValidation;
using ItauInvestmentBroker.Application.DTOs.Cesta;

namespace ItauInvestmentBroker.Application.Validators;

public class CestaRequestValidator : AbstractValidator<CestaRequest>
{
    public CestaRequestValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome da cesta é obrigatório.");

        RuleFor(x => x.Itens)
            .Must(i => i.Count == 5)
            .WithMessage(x => $"A cesta deve conter exatamente 5 ativos. Quantidade informada: {x.Itens.Count}.");

        RuleFor(x => x.Itens)
            .Must(i => i.Sum(item => item.Percentual) == 100)
            .WithMessage(x => $"A soma dos percentuais deve ser exatamente 100%. Soma atual: {x.Itens.Sum(i => i.Percentual)}%.");

        RuleFor(x => x.Itens)
            .Must(i => i.Select(item => item.Ticker).Distinct().Count() == i.Count)
            .WithMessage("Não são permitidos tickers duplicados na cesta.");
    }
}
