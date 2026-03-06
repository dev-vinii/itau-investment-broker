namespace ItauInvestmentBroker.Application.DTOs.Cesta;

public record CestaRequest(string Nome, List<ItemCestaRequest> Itens);

public record ItemCestaRequest(string Ticker, decimal Percentual);
