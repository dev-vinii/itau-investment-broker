namespace ItauInvestmentBroker.Application.Features.Rentabilidade.DTOs;

public record RentabilidadeResponse(
    long ClienteId,
    string Nome,
    decimal ValorInvestidoTotal,
    decimal ValorAtualTotal,
    decimal PlTotal,
    decimal RentabilidadePercentual,
    List<AtivoRentabilidadeResponse> Ativos
);

public record AtivoRentabilidadeResponse(
    string Ticker,
    int Quantidade,
    decimal PrecoMedio,
    decimal CotacaoAtual,
    decimal ValorAtual,
    decimal Pl,
    decimal ComposicaoPercentual
);
