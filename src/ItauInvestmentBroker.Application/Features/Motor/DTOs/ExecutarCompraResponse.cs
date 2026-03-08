namespace ItauInvestmentBroker.Application.Features.Motor.DTOs;

public record ExecutarCompraResponse(
    DateTime DataExecucao,
    int TotalClientes,
    decimal TotalConsolidado,
    List<OrdemCompraResponse> OrdensCompra,
    List<DistribuicaoClienteResponse> Distribuicoes,
    List<ResiduoMasterResponse> ResiduosCustMaster,
    int EventosIRPublicados,
    string Mensagem
);

public record OrdemCompraResponse(
    string Ticker,
    int QuantidadeTotal,
    List<DetalheOrdemResponse> Detalhes,
    decimal PrecoUnitario,
    decimal ValorTotal
);

public record DetalheOrdemResponse(
    string Tipo,
    string Ticker,
    int Quantidade
);

public record DistribuicaoClienteResponse(
    long ClienteId,
    string Nome,
    decimal ValorAporte,
    List<AtivoDistribuidoResponse> Ativos
);

public record AtivoDistribuidoResponse(
    string Ticker,
    int Quantidade
);

public record ResiduoMasterResponse(
    string Ticker,
    int Quantidade
);
