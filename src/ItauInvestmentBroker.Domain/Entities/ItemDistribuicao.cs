namespace ItauInvestmentBroker.Domain.Entities;

public class ItemDistribuicao : BaseEntity
{
    public required string Ticker { get; set; }
    public int Quantidade { get; set; }

    public long DistribuicaoId { get; set; }
    public Distribuicao? Distribuicao { get; set; }

    public long ContaGraficaId { get; set; }
    public ContaGrafica? ContaGrafica { get; set; }
}
