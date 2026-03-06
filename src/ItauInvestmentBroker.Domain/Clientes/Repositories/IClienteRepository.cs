using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Common;

namespace ItauInvestmentBroker.Domain.Clientes.Repositories;

public interface IClienteRepository : IBaseRepository<Cliente>
{
    Task<Cliente?> FindByCpf(string cpf, CancellationToken cancellationToken = default);
    Task<IEnumerable<Cliente>> FindAtivos(CancellationToken cancellationToken = default);
}
