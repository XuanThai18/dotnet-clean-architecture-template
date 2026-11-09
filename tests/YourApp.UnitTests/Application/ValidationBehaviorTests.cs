using FluentValidation;
using MediatR;
using YourApp.Application.Behaviors;
using YourApp.Application.Common.Exceptions;
using YourApp.Application.Products;
using YourApp.Domain.Shared;

namespace YourApp.UnitTests.Application;

public sealed class ValidationBehaviorTests
{
    private static readonly IValidator<CreateProductCommand>[] Validators = [new CreateProductCommandValidator()];

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsWithErrorsAndSkipsHandler()
    {
        var behavior = new ValidationBehavior<CreateProductCommand, Result<Guid>>(Validators);
        var handlerCalled = false;

        var ex = await Assert.ThrowsAsync<CustomValidationException>(() => behavior.Handle(
            new CreateProductCommand("", "bad sku", -1),
            _ => { handlerCalled = true; return Task.FromResult<Result<Guid>>(Guid.NewGuid()); },
            TestContext.Current.CancellationToken));

        Assert.False(handlerCalled);
        Assert.Contains(nameof(CreateProductCommand.Name), ex.Errors.Keys);
        Assert.Contains(nameof(CreateProductCommand.Sku), ex.Errors.Keys);
        Assert.Contains(nameof(CreateProductCommand.Price), ex.Errors.Keys);
    }

    [Fact]
    public async Task Handle_ValidRequest_PassesCancellationTokenToHandler()
    {
        var behavior = new ValidationBehavior<CreateProductCommand, Result<Guid>>(Validators);
        using var cts = new CancellationTokenSource();
        CancellationToken received = default;

        RequestHandlerDelegate<Result<Guid>> next = token =>
        {
            received = token;
            return Task.FromResult<Result<Guid>>(Guid.NewGuid());
        };

        await behavior.Handle(new CreateProductCommand("Keyboard", "KB-01", 1), next, cts.Token);

        Assert.Equal(cts.Token, received);
    }
}
