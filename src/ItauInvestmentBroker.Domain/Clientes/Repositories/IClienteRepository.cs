using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Common;

namespace ItauInvestmentBroker.Domain.Clientes.Repositories;

public interface IClienteRepository : IBaseRepository<Cliente>
{
    Task<Cliente?> FindByCpf(string cpf, CancellationToken cancellationToken = default);
    Task<IEnumerable<Cliente>> FindAtivos(CancellationToken cancellationToken = default);
    Task<List<Cliente>> FindAtivosPaginado(int skip, int take, CancellationToken cancellationToken = default);
    Task<int> CountAtivos(CancellationToken cancellationToken = default);
    Task<decimal> SomarValorMensalAtivos(CancellationToken cancellationToken = default);
}
