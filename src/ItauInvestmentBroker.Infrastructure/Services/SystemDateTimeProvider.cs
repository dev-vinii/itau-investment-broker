using ItauInvestmentBroker.Application.Interfaces;

namespace ItauInvestmentBroker.Infrastructure.Services;

public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
