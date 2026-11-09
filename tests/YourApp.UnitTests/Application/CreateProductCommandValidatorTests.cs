using FluentValidation.TestHelper;
using YourApp.Application.Products;
using YourApp.Domain.Products;

namespace YourApp.UnitTests.Application;

public sealed class CreateProductCommandValidatorTests
{
    private readonly CreateProductCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_HasNoErrors() =>
        _validator.TestValidate(new CreateProductCommand("Keyboard", "KB-01", 10)).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("KB 01")]
    [InlineData("KB_01")]
    public void Validate_InvalidSku_HasSkuError(string sku) =>
        _validator.TestValidate(new CreateProductCommand("Keyboard", sku, 10)).ShouldHaveValidationErrorFor(x => x.Sku);

    [Fact]
    public void Validate_NameTooLong_HasNameError() =>
        _validator.TestValidate(new CreateProductCommand(new string('a', Product.NameMaxLength + 1), "KB-01", 10))
            .ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void Validate_NegativePrice_HasPriceError() =>
        _validator.TestValidate(new CreateProductCommand("Keyboard", "KB-01", -0.01m)).ShouldHaveValidationErrorFor(x => x.Price);
}
