using YourApp.Domain.Shared;

namespace YourApp.Domain.Products;

public static class ProductErrors
{
    public static readonly Error NegativePrice =
        Error.Validation("Products.NegativePrice", "Price must not be negative.");

    public static Error NotFound(Guid id) =>
        Error.NotFound("Products.NotFound", $"Product '{id}' was not found.");

    public static Error SkuAlreadyExists(string sku) =>
        Error.Conflict("Products.SkuAlreadyExists", $"SKU '{sku}' is already in use.");
}
