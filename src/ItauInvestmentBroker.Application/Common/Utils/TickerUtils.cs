namespace ItauInvestmentBroker.Application.Common.Utils;

public static class TickerUtils
{
    public static string Normalize(string ticker) => ticker.Trim().ToUpperInvariant();
}
