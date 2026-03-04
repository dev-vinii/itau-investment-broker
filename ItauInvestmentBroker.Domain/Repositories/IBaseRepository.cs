using ItauInvestmentBroker.Domain.Entities;

namespace ItauInvestmentBroker.Domain.Repositories;

public interface IBaseRepository<T> where T : BaseEntity
{
    Task<T?> FindById(long id);
    Task<IEnumerable<T>> FindAll();
    Task<T> Save(T entity);
    Task Delete(long id);
}
