namespace ItauInvestmentBroker.Application.Configuration;

public class MotorSettings
{
    public const string SectionName = "Motor";

    public decimal DivisorMensal { get; set; } = 3m;
    public int LotePadrao { get; set; } = 100;
    public decimal AliquotaIrDedoDuro { get; set; } = 0.00005m;
    public decimal AliquotaIrVenda { get; set; } = 0.20m;
    public decimal LimiteIsencaoVendas { get; set; } = 20_000m;
    public decimal LimiarDesvioPontos { get; set; } = 5m;
    public string TopicoIrDedoDuro { get; set; } = "ir-dedo-duro";
    public string TopicoIrVenda { get; set; } = "ir-venda";
}
