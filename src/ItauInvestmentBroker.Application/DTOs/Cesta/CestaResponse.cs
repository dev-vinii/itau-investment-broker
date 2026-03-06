namespace ItauInvestmentBroker.Application.DTOs.Cesta;

public record CestaResponse(
    long CestaId,
    string Nome,
    bool Ativa,
    DateTime DataCriacao,
    DateTime? DataDesativacao,
    List<ItemCestaResponse> Itens
);

public record ItemCestaResponse(string Ticker, decimal Percentual);
