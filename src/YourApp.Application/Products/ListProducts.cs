using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using YourApp.Application.Abstractions;
using YourApp.Application.Common.Paging;
using YourApp.Domain.Products;
using YourApp.Domain.Shared;

namespace YourApp.Application.Products;

public sealed record ListProductsQuery(int Page = 1, int PageSize = 20, string? Search = null)
    : IRequest<Result<PagedResult<ProductResponse>>>;

public sealed class ListProductsQueryValidator : AbstractValidator<ListProductsQuery>
{
    public const int MaxPageSize = 100;

    public ListProductsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public sealed class ListProductsQueryHandler(IReadDbContext db)
    : IRequestHandler<ListProductsQuery, Result<PagedResult<ProductResponse>>>
{
    public async Task<Result<PagedResult<ProductResponse>>> Handle(ListProductsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Query<Product>();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // Contains → LIKE @p trên Postgres; EF tự escape "%"/"_" trong giá trị tham số nên không thành wildcard.
#pragma warning disable CA1304, CA1311, CA1862 // ToLower trong expression được dịch sang lower() của SQL, không chạy culture .NET
            var term = request.Search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Sku.ToLower().Contains(term));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var total = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(ProductResponse.Projection)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductResponse>(items, request.Page, request.PageSize, total);
    }
}
