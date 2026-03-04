using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;
using ItauInvestmentBroker.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ItauInvestmentBroker.Infrastructure.Repositories;

public class BaseRepository<T> : IBaseRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> DbSet;

    public BaseRepository(AppDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public async Task<T?> FindById(long id)
    {
        return await DbSet.FindAsync(id);
    }

    public async Task<IEnumerable<T>> FindAll()
    {
        return await DbSet.ToListAsync();
    }

    public async Task<T> Save(T entity)
    {
        if (entity.Id == 0)
            await DbSet.AddAsync(entity);
        else
            DbSet.Update(entity);

        await Context.SaveChangesAsync();
        return entity;
    }

    public async Task Delete(long id)
    {
        var entity = await FindById(id);
        if (entity is not null)
        {
            DbSet.Remove(entity);
            await Context.SaveChangesAsync();
        }
    }
}
