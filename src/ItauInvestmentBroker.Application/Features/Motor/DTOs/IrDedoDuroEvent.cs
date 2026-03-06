namespace ItauInvestmentBroker.Application.Features.Motor.DTOs;

public record IrDedoDuroEvent(
    string Tipo,
    long ClienteId,
    string Cpf,
    string Ticker,
    string TipoOperacao,
    int Quantidade,
    decimal PrecoUnitario,
    decimal ValorOperacao,
    decimal Aliquota,
    decimal ValorIR,
    DateTime DataOperacao
);
