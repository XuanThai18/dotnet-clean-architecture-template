using Microsoft.EntityFrameworkCore;
using YourApp.Domain.Products;
using YourApp.Infrastructure.Persistence.Contexts;

namespace YourApp.Infrastructure.Persistence.Repositories;

internal sealed class ProductRepository(ApplicationDbContext dbContext) : IProductRepository
{
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<Product>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken) =>
        dbContext.Set<Product>().AnyAsync(p => p.Sku == sku, cancellationToken);

    public void Add(Product product) => dbContext.Set<Product>().Add(product);

    public void Remove(Product product) => dbContext.Set<Product>().Remove(product);
}
