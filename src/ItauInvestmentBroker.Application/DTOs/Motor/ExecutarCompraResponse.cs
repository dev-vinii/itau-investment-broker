namespace ItauInvestmentBroker.Application.DTOs.Motor;

public record ExecutarCompraResponse(
    long OrdemCompraId,
    DateTime DataExecucao,
    decimal ValorTotal,
    int TotalClientes,
    List<ItemCompraResponse> Itens
);

public record ItemCompraResponse(
    string Ticker,
    int QuantidadeLote,
    int QuantidadeFracionario,
    int QuantidadeTotal,
    decimal PrecoUnitario,
    decimal ValorTotal
);
