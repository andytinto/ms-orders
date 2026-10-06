using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Orders.Application.Common.Interfaces;
using Orders.Application.Constants;
using Orders.Domain.Common;
using Orders.Domain.Models;

namespace Orders.Persistance.DbContext
{
    public class OrdersContext : Microsoft.EntityFrameworkCore.DbContext, IOrdersDbContext
    {
        private readonly ICurrentUserService? _currentUserService;

        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
        public DbSet<OrderConfig> OrderConfig => Set<OrderConfig>();

        public OrdersContext(DbContextOptions<OrdersContext> options, ICurrentUserService? currentUserService = null)
            : base(options)
        {
            _currentUserService = currentUserService;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersContext).Assembly);

            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.OrderId);
                entity.Property(e => e.RowVersion).IsRowVersion();
                entity.HasIndex(e => e.IdempotencyKey)
                    .IsUnique()
                    .HasFilter("[IdempotencyKey] IS NOT NULL");
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RowVersion).IsRowVersion();
                entity.ToTable(t => t.HasCheckConstraint("CK_Product_StockQuantity_NonNegative", "[StockQuantity] >= 0"));
            });

            modelBuilder.Entity<IdempotencyRecord>(entity =>
            {
                entity.HasKey(e => e.Key);
            });

            base.OnModelCreating(modelBuilder);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                return Task.FromResult<IDbContextTransaction>(new InMemoryTransaction());
            }
            return Database.BeginTransactionAsync(cancellationToken);
        }

        private class InMemoryTransaction : IDbContextTransaction
        {
            public Guid TransactionId { get; } = Guid.NewGuid();
            public void Commit() { }
            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public void Rollback() { }
            public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        public Task<int> ExecuteSqlRawAsync(string sql, IEnumerable<object> parameters, CancellationToken cancellationToken = default)
        {
            return Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var currentUsername = !string.IsNullOrWhiteSpace(_currentUserService?.EmployeeId)
                ? _currentUserService.EmployeeId
                : GeneralConstant.DEFAULT_USER;

            var now = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedBy = currentUsername;
                        entry.Entity.CreatedDate = now;
                        entry.Entity.ModifiedBy = currentUsername;
                        entry.Entity.ModifiedDate = now;
                        entry.Entity.IsDeleted = false;
                        break;

                    case EntityState.Modified:
                        entry.Entity.ModifiedBy = currentUsername;
                        entry.Entity.ModifiedDate = now;
                        break;

                    case EntityState.Deleted:
                        entry.State = EntityState.Modified;
                        entry.Entity.IsDeleted = true;
                        entry.Entity.ModifiedBy = currentUsername;
                        entry.Entity.ModifiedDate = now;
                        break;
                }
            }

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is Product product && (entry.State == EntityState.Added || entry.State == EntityState.Modified))
                {
                    product.RowVersion = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
                }
                else if (entry.Entity is Order order && (entry.State == EntityState.Added || entry.State == EntityState.Modified))
                {
                    order.RowVersion = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
