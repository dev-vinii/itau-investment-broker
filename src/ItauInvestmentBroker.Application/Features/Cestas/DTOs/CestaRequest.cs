namespace ItauInvestmentBroker.Application.Features.Cestas.DTOs;

public record CestaRequest(string Nome, List<ItemCestaRequest> Itens);

public record ItemCestaRequest(string Ticker, decimal Percentual);
