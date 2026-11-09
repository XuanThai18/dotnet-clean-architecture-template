using NSubstitute;
using NSubstitute.ExceptionExtensions;
using YourApp.Application.Abstractions;
using YourApp.Application.Common.Exceptions;
using YourApp.Application.Products;
using YourApp.Domain.Products;
using YourApp.Domain.Shared;

namespace YourApp.UnitTests.Application;

public sealed class CreateProductCommandHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateProductCommandHandler _handler;

    public CreateProductCommandHandlerTests() => _handler = new CreateProductCommandHandler(_products, _unitOfWork);

    [Fact]
    public async Task Handle_NewSku_AddsProductAndSaves()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _handler.Handle(new CreateProductCommand("Keyboard", "kb-01", 10), ct);

        Assert.True(result.IsSuccess);
        _products.Received(1).Add(Arg.Is<Product>(p => p.Id == result.Value && p.Sku == "KB-01"));
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }

    [Fact]
    public async Task Handle_ExistingSku_ReturnsConflictWithoutSaving()
    {
        var ct = TestContext.Current.CancellationToken;
        _products.SkuExistsAsync("KB-01", ct).Returns(true);

        var result = await _handler.Handle(new CreateProductCommand("Keyboard", "kb-01", 10), ct);

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConcurrentInsertHitsUniqueIndex_ReturnsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        _unitOfWork.SaveChangesAsync(ct).ThrowsAsync(new UniqueConstraintException("duplicate"));

        var result = await _handler.Handle(new CreateProductCommand("Keyboard", "kb-01", 10), ct);

        Assert.Equal(ProductErrors.SkuAlreadyExists("KB-01"), result.Error);
    }
}
