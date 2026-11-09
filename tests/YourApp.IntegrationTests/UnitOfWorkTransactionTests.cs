using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using YourApp.Application.Abstractions;
using YourApp.Domain.Products;
using YourApp.Domain.Shared;
using YourApp.Infrastructure.Persistence.Contexts;

namespace YourApp.IntegrationTests;

/// <summary>Kiểm ExecuteInTransactionAsync trên Postgres thật: commit, rollback, gọi lồng, retry.</summary>
public sealed class UnitOfWorkTransactionTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Completes_CommitsAllSaveChangesCalls()
    {
        var tag = NewTag();
        await using var scope = factory.Services.CreateAsyncScope();
        var (uow, products) = Resolve(scope);

        await uow.ExecuteInTransactionAsync(async ct =>
        {
            products.Add(NewProduct(tag, 1));
            await uow.SaveChangesAsync(ct);
            products.Add(NewProduct(tag, 2));
            await uow.SaveChangesAsync(ct);
        }, cancellationToken: Ct);

        Assert.Equal(2, await CountAsync(tag));
    }

    [Fact]
    public async Task Throws_RollsBackEarlierSaveChangesAndKeepsExceptionType()
    {
        var tag = NewTag();
        await using var scope = factory.Services.CreateAsyncScope();
        var (uow, products) = Resolve(scope);

        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync(async ct =>
        {
            products.Add(NewProduct(tag, 1));
            await uow.SaveChangesAsync(ct);          // đã ghi xuống DB trong transaction...
            throw new InvalidOperationException("boom"); // ...nhưng phải bị rollback
        }, cancellationToken: Ct));

        Assert.Equal(0, await CountAsync(tag));
    }

    [Fact]
    public async Task ReturnsFailureResult_RollsBackWithoutException()
    {
        var tag = NewTag();
        await using var scope = factory.Services.CreateAsyncScope();
        var (uow, products) = Resolve(scope);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var result = await uow.ExecuteInTransactionAsync<Result>(async ct =>
        {
            products.Add(NewProduct(tag, 1));
            await uow.SaveChangesAsync(ct);
            return Error.Conflict("Stock.Insufficient", "Not enough stock.");
        }, cancellationToken: Ct);

        Assert.True(result.IsFailure);
        Assert.Equal(0, await CountAsync(tag));
        Assert.Empty(db.ChangeTracker.Entries()); // không còn entity "treo" để lỡ tay SaveChanges lần nữa
    }

    [Fact]
    public async Task Nested_InnerJoinsOuterTransaction_OuterFailureRollsBackBoth()
    {
        var tag = NewTag();
        await using var scope = factory.Services.CreateAsyncScope();
        var (uow, products) = Resolve(scope);

        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync(async ct =>
        {
            products.Add(NewProduct(tag, 1));
            await uow.SaveChangesAsync(ct);

            // Lời gọi trong (vd một service khác) không được tự commit riêng.
            await uow.ExecuteInTransactionAsync(async innerCt =>
            {
                products.Add(NewProduct(tag, 2));
                await uow.SaveChangesAsync(innerCt);
            }, cancellationToken: ct);

            throw new InvalidOperationException("outer fails after inner finished");
        }, cancellationToken: Ct));

        Assert.Equal(0, await CountAsync(tag));
    }

    [Fact]
    public async Task TransientFailure_RetriesWithCleanChangeTracker()
    {
        var tag = NewTag();
        await using var scope = factory.Services.CreateAsyncScope();
        var (uow, products) = Resolve(scope);
        var attempts = 0;

        await uow.ExecuteInTransactionAsync(async ct =>
        {
            attempts++;
            products.Add(NewProduct(tag, attempts));
            if (attempts == 1)
            {
                // Lỗi mạng giả lập TRƯỚC SaveChanges: entity lần 1 vẫn nằm trong tracker ở trạng thái Added.
                throw new NpgsqlException("simulated network blip", new TimeoutException());
            }

            await uow.SaveChangesAsync(ct);
        }, cancellationToken: Ct);

        Assert.Equal(2, attempts);
        // Không xoá tracker khi retry thì lần 2 sẽ insert cả entity của lần 1 → 2 dòng.
        Assert.Equal(1, await CountAsync(tag));
    }

    private static (IUnitOfWork UnitOfWork, IProductRepository Products) Resolve(AsyncServiceScope scope) =>
        (scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), scope.ServiceProvider.GetRequiredService<IProductRepository>());

    private async Task<int> CountAsync(string tag)
    {
        // Scope mới: đọc đúng trạng thái đã commit trong DB, không lẫn Change Tracker của test.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<Product>().IgnoreQueryFilters().CountAsync(p => p.Sku.StartsWith(tag), Ct);
    }

    private static string NewTag() => "TX-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    private static Product NewProduct(string tag, int index) => Product.Create($"Item {index}", $"{tag}-{index}", 1).Value;
}
