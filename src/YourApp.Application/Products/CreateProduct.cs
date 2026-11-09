using FluentValidation;
using MediatR;
using YourApp.Application.Abstractions;
using YourApp.Application.Common.Exceptions;
using YourApp.Domain.Products;
using YourApp.Domain.Shared;

namespace YourApp.Application.Products;

public sealed record CreateProductCommand(string Name, string Sku, decimal Price) : IRequest<Result<Guid>>;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Product.NameMaxLength);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(Product.SkuMaxLength)
            .Matches("^[A-Za-z0-9-]+$").WithMessage("SKU may contain only letters, digits and '-'.");
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateProductCommandHandler(IProductRepository products, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateProductCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var sku = Product.NormalizeSku(request.Sku);
        if (await products.SkuExistsAsync(sku, cancellationToken))
            return ProductErrors.SkuAlreadyExists(sku);

        var created = Product.Create(request.Name, sku, request.Price);
        if (created.IsFailure)
            return created.Error;

        products.Add(created.Value);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            // Request song song đã chèn cùng SKU sau bước kiểm ở trên.
            return ProductErrors.SkuAlreadyExists(sku);
        }

        return created.Value.Id;
    }
}
