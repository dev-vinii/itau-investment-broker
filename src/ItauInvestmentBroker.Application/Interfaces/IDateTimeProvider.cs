namespace ItauInvestmentBroker.Application.Interfaces;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
