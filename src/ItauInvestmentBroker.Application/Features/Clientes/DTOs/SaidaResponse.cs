namespace ItauInvestmentBroker.Application.Features.Clientes.DTOs;

public record SaidaResponse(
    long ClienteId,
    string Nome,
    string Cpf,
    string Email,
    DateTime DataAdesao,
    DateTime DataSaida
);
