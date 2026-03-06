using ItauInvestmentBroker.Domain.Entities;

namespace ItauInvestmentBroker.Domain.Repositories;

public interface IClienteRepository : IBaseRepository<Cliente>
{
    Task<Cliente?> FindByCpf(string cpf, CancellationToken cancellationToken = default);
    Task<IEnumerable<Cliente>> FindAtivos(CancellationToken cancellationToken = default);
}
