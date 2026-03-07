namespace ItauInvestmentBroker.Application.Features.Clientes.DTOs;

public record ValorMensalResponse(
    long ClienteId,
    decimal ValorMensalAnterior,
    decimal ValorMensalNovo,
    DateTime DataAlteracao,
    string Mensagem
);
