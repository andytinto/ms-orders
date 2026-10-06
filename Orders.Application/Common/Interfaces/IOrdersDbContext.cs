using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Orders.Domain.Models;

namespace Orders.Application.Common.Interfaces
{
    public interface IOrdersDbContext
    {
        DbSet<Order> Orders { get; }
        DbSet<OrderDetail> OrderDetails { get; }
        DbSet<Product> Products { get; }
        DbSet<IdempotencyRecord> IdempotencyRecords { get; }
        DbSet<OrderConfig> OrderConfig { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task<int> ExecuteSqlRawAsync(string sql, IEnumerable<object> parameters, CancellationToken cancellationToken = default);
    }
}
