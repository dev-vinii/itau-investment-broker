using ItauInvestmentBroker.Domain.Entities;

namespace ItauInvestmentBroker.Domain.Repositories;

public interface IClienteRepository : IBaseRepository<Cliente>
{
    Task<Cliente?> FindByCpf(string cpf);
    Task<IEnumerable<Cliente>> FindAtivos();
}
