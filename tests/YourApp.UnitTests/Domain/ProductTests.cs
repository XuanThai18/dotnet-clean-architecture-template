using YourApp.Domain.Products;
using YourApp.Domain.Shared;

namespace YourApp.UnitTests.Domain;

public sealed class ProductTests
{
    [Fact]
    public void Create_WithValidData_NormalizesSkuAndTrimsName()
    {
        var result = Product.Create("  Keyboard  ", " kb-01 ", 49.90m);

        Assert.True(result.IsSuccess);
        Assert.Equal("Keyboard", result.Value.Name);
        Assert.Equal("KB-01", result.Value.Sku);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
    }

    [Fact]
    public void Create_WithNegativePrice_ReturnsValidationError()
    {
        var result = Product.Create("Keyboard", "KB-01", -1);

        Assert.True(result.IsFailure);
        Assert.Equal(ProductErrors.NegativePrice, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Create_UsesUuidVersion7()
    {
        var product = Product.Create("A", "A-1", 1).Value;

        // v7 có timestamp ở đầu → insert tăng dần, không phân mảnh index như Guid.NewGuid() (v4).
        Assert.Equal(7, product.Id.Version);
    }

    [Fact]
    public void Update_WithNegativePrice_KeepsOriginalValues()
    {
        var product = Product.Create("Keyboard", "KB-01", 10).Value;

        var result = product.Update("Mouse", -5);

        Assert.True(result.IsFailure);
        Assert.Equal("Keyboard", product.Name);
        Assert.Equal(10, product.Price);
    }
}
