namespace ItauInvestmentBroker.Application.DTOs.Cliente;

public record AdesaoRequest(string Nome, string Cpf, string Email, decimal ValorMensal);
