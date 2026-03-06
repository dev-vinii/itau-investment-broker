using ItauInvestmentBroker.Application.Common.Interfaces;

namespace ItauInvestmentBroker.Infrastructure.Services;

public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
