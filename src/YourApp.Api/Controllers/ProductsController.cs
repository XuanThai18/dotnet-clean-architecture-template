using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YourApp.Api.Contracts.Products;
using YourApp.Api.Extensions;
using YourApp.Application.Common.Paging;
using YourApp.Application.Products;

namespace YourApp.Api.Controllers;

[ApiVersion(1.0)]
public sealed class ProductsController(ISender sender) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType<PagedResult<ProductResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        CancellationToken cancellationToken = default) =>
        HandleResult(await sender.Send(new ListProductsQuery(page, pageSize, search), cancellationToken));

    [HttpGet("price-stats")]
    [ProducesResponseType<IReadOnlyList<ProductPriceStatsResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PriceStats(CancellationToken cancellationToken) =>
        HandleResult(await sender.Send(new GetProductPriceStatsQuery(), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await sender.Send(new GetProductByIdQuery(id), cancellationToken));

    [HttpPost]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateProductCommand(request.Name, request.Sku, request.Price), cancellationToken);
        if (result.IsFailure)
            return ToProblem(result.Error);

        // Route có {version:apiVersion} nên phải truyền lại version khi sinh link Location.
        return CreatedAtAction(nameof(GetById), new { id = result.Value, version = RouteData.Values["version"] }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, UpdateProductRequest request, CancellationToken cancellationToken) =>
        HandleResult(await sender.Send(new UpdateProductCommand(id, request.Name, request.Price), cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.RequireAdminRole)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await sender.Send(new DeleteProductCommand(id), cancellationToken));
}
