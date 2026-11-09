using YourApp.Domain.Common;
using YourApp.Domain.Shared;

namespace YourApp.Domain.Products;

/// <summary>Entity mẫu: bất biến nằm trong entity, setter private, tạo qua factory trả Result.</summary>
public sealed class Product : IAuditableEntity, ISoftDeletable
{
    public const int NameMaxLength = 200;
    public const int SkuMaxLength = 50;

    private Product()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public decimal Price { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public static Result<Product> Create(string name, string sku, decimal price)
    {
        if (price < 0)
            return ProductErrors.NegativePrice;

        return new Product
        {
            // UUID v7 tăng dần theo thời gian → index B-tree không bị phân mảnh như Guid ngẫu nhiên.
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Sku = NormalizeSku(sku),
            Price = price
        };
    }

    public Result Update(string name, decimal price)
    {
        if (price < 0)
            return ProductErrors.NegativePrice;

        Name = name.Trim();
        Price = price;
        return Result.Success();
    }

    public static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();
}
