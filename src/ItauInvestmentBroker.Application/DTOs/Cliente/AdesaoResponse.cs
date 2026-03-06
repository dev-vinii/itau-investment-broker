namespace ItauInvestmentBroker.Application.DTOs.Cliente;

public record AdesaoResponse(
    long ClienteId,
    string Nome,
    string Cpf,
    string Email,
    decimal ValorMensal,
    DateTime DataAdesao,
    string NumeroConta
);
