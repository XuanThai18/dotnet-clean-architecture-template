using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using YourApp.Application.Abstractions;
using YourApp.Application.Common.Exceptions;
using YourApp.Domain.Common;

namespace YourApp.Infrastructure.Persistence.Contexts;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options), IReadDbContext
{
    IQueryable<TEntity> IReadDbContext.Query<TEntity>() => Set<TEntity>().AsNoTracking();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new UniqueConstraintException(pg.MessageText, ex) { ConstraintName = pg.ConstraintName };
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        ApplySoftDeleteQueryFilter(modelBuilder);
    }

    private static void ApplySoftDeleteQueryFilter(ModelBuilder modelBuilder)
    {
        // Cần xem cả bản ghi đã xoá thì dùng IgnoreQueryFilters().
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(ISoftDeletable).IsAssignableFrom(t.ClrType) && t.BaseType is null))
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var body = Expression.Not(Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)));
            entityType.SetQueryFilter(Expression.Lambda(body, parameter));
        }
    }
}
