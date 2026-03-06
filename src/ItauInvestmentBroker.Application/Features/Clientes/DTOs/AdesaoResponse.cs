namespace ItauInvestmentBroker.Application.Features.Clientes.DTOs;

public record AdesaoResponse(
    long ClienteId,
    string Nome,
    string Cpf,
    string Email,
    decimal ValorMensal,
    DateTime DataAdesao,
    string NumeroConta
);
