namespace ItauInvestmentBroker.Domain.Entities;

public class Cliente : BaseEntity
{
    public required string Nome { get; set; }
    public required string Cpf { get; set; }
    public required string Email { get; set; }
    public decimal ValorMensal { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime DataAdesao { get; set; } = DateTime.UtcNow;

    public ContaGrafica? ContaGrafica { get; set; }
}
