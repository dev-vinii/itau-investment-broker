using ItauInvestmentBroker.Application.Common.Models;

namespace ItauInvestmentBroker.Application.Common.Interfaces;

public interface ICotacaoService
{
    Cotacao? ObterCotacao(string ticker);
    IReadOnlyDictionary<string, Cotacao> ObterTodasCotacoes();
}
