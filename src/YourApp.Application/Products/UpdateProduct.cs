using FluentValidation;
using MediatR;
using YourApp.Application.Abstractions;
using YourApp.Domain.Products;
using YourApp.Domain.Shared;

namespace YourApp.Application.Products;

public sealed record UpdateProductCommand(Guid Id, string Name, decimal Price) : IRequest<Result>;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Product.NameMaxLength);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateProductCommandHandler(IProductRepository products, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateProductCommand, Result>
{
    public async Task<Result> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(request.Id, cancellationToken);
        if (product is null)
            return ProductErrors.NotFound(request.Id);

        var updated = product.Update(request.Name, request.Price);
        if (updated.IsFailure)
            return updated;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
