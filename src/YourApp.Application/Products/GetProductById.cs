using MediatR;
using Microsoft.EntityFrameworkCore;
using YourApp.Application.Abstractions;
using YourApp.Domain.Products;
using YourApp.Domain.Shared;

namespace YourApp.Application.Products;

public sealed record GetProductByIdQuery(Guid Id) : IRequest<Result<ProductResponse>>;

/// <summary>Mẫu đường đọc mặc định: LINQ không tracking + Select chỉ các cột cần.</summary>
public sealed class GetProductByIdQueryHandler(IReadDbContext db)
    : IRequestHandler<GetProductByIdQuery, Result<ProductResponse>>
{
    public async Task<Result<ProductResponse>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await db.Query<Product>()
            .Where(p => p.Id == request.Id)
            .Select(ProductResponse.Projection)
            .FirstOrDefaultAsync(cancellationToken);

        return product is null ? ProductErrors.NotFound(request.Id) : product;
    }
}
