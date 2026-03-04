using ItauInvestmentBroker.Domain.Entities;

namespace ItauInvestmentBroker.Domain.Repositories;

public interface IContaGraficaRepository : IBaseRepository<ContaGrafica>
{
    Task<ContaGrafica?> FindByClienteId(long clienteId);
    Task<IEnumerable<ContaGrafica>> FindMaster();
    Task<ContaGrafica?> FindByNumeroConta(string numeroConta);
}
