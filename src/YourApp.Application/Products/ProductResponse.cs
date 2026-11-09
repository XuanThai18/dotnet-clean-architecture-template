using System.Linq.Expressions;
using YourApp.Domain.Products;

namespace YourApp.Application.Products;

public sealed class ProductResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>
    /// Phép chiếu dùng chung cho mọi query: EF dịch thành SELECT đúng các cột này.
    /// Là Expression (không phải hàm map thường) nên chạy được trong SQL, không nạp entity về RAM.
    /// </summary>
    public static readonly Expression<Func<Product, ProductResponse>> Projection = p => new ProductResponse
    {
        Id = p.Id,
        Name = p.Name,
        Sku = p.Sku,
        Price = p.Price,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}
