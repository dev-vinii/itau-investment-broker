namespace ItauInvestmentBroker.Application.Common.Constants;

public static class BusinessConstants
{
    public const int CpfLength = 11;
    public const decimal ValorMensalMinimo = 100m;

    public const int QuantidadeAtivosCesta = 5;
    public const decimal PercentualTotalCesta = 100m;
    public const decimal ToleranciaPercentualCesta = 0.01m;

    public const int CasasDecimaisMonetarias = 2;

    public const int TamanhoNumeroContaFilhote = 10;
    public const string FormatoGuidSemHifen = "N";
    public const string FormatoMesReferencia = "yyyy-MM";
}
