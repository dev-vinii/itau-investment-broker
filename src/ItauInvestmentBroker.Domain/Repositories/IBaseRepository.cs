using ItauInvestmentBroker.Domain.Entities;

namespace ItauInvestmentBroker.Domain.Repositories;

public interface IBaseRepository<T> where T : BaseEntity
{
    Task<T?> FindById(long id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> FindAll(CancellationToken cancellationToken = default);
    void Add(T entity);
    void Update(T entity);
    void Delete(T entity);
}
