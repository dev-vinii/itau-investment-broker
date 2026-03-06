using ItauInvestmentBroker.Application.Models;

namespace ItauInvestmentBroker.Application.Interfaces;

public interface ICotacaoService
{
    Cotacao? ObterCotacao(string ticker);
    IReadOnlyDictionary<string, Cotacao> ObterTodasCotacoes();
}
