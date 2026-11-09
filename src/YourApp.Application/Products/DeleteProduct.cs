using MediatR;
using YourApp.Application.Abstractions;
using YourApp.Domain.Products;
using YourApp.Domain.Shared;

namespace YourApp.Application.Products;

/// <summary>Xoá mềm: SoftDeleteInterceptor đổi Remove() thành cập nhật is_deleted.</summary>
public sealed record DeleteProductCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteProductCommandHandler(IProductRepository products, IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteProductCommand, Result>
{
    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(request.Id, cancellationToken);
        if (product is null)
            return ProductErrors.NotFound(request.Id);

        products.Remove(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
