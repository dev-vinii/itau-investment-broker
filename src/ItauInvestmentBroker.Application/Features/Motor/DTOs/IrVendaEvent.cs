namespace ItauInvestmentBroker.Application.Features.Motor.DTOs;

public record IrVendaEvent(
    string Tipo,
    long ClienteId,
    string Cpf,
    string MesReferencia,
    decimal TotalVendasMes,
    decimal LucroLiquido,
    decimal Aliquota,
    decimal ValorIR,
    List<IrVendaDetalheEvent> Detalhes,
    DateTime DataCalculo
);

public record IrVendaDetalheEvent(
    string Ticker,
    int Quantidade,
    decimal PrecoVenda,
    decimal PrecoMedio,
    decimal Lucro
);
