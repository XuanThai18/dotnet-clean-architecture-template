namespace YourApp.Domain.Products;

/// <summary>Chỉ phục vụ phía ghi (command). Phía đọc (query) dùng Dapper qua ISqlConnectionFactory.</summary>
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken);
    void Add(Product product);
    void Remove(Product product);
}
