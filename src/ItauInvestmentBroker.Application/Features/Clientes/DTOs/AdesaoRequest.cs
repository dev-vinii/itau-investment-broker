namespace ItauInvestmentBroker.Application.Features.Clientes.DTOs;

public record AdesaoRequest(string Nome, string Cpf, string Email, decimal ValorMensal);
