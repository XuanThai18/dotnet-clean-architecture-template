using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using YourApp.Api.Contracts.Products;
using YourApp.Application.Common.Paging;
using YourApp.Application.Products;

namespace YourApp.IntegrationTests;

public sealed class ProductsEndpointsTests(ApiFactory factory)
{
    private const string BaseUrl = "/api/v1/products";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_WithoutToken_Returns401ProblemDetails()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(BaseUrl, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Create_ThenGetById_ReturnsCreatedProduct()
    {
        using var client = factory.CreateAuthenticatedClient();
        var sku = NewSku();

        using var created = await client.PostAsJsonAsync(BaseUrl, new CreateProductRequest(" Keyboard ", sku, 49.9m), Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);

        var product = await client.GetFromJsonAsync<ProductResponse>(created.Headers.Location, Ct);

        Assert.NotNull(product);
        Assert.Equal("Keyboard", product.Name);
        Assert.Equal(sku.ToUpperInvariant(), product.Sku);
        Assert.Equal(49.9m, product.Price);
        Assert.True(product.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-1), "created_at phải do interceptor điền");
        Assert.Null(product.UpdatedAt);
    }

    [Fact]
    public async Task Create_InvalidBody_Returns400WithFieldErrors()
    {
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.PostAsJsonAsync(BaseUrl, new CreateProductRequest("", "bad sku", -1), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadProblemAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Name", out _));
        Assert.True(errors.TryGetProperty("Sku", out _));
        Assert.True(errors.TryGetProperty("Price", out _));
    }

    [Fact]
    public async Task Create_DuplicateSku_Returns409WithErrorCode()
    {
        using var client = factory.CreateAuthenticatedClient();
        var sku = NewSku();
        (await client.PostAsJsonAsync(BaseUrl, new CreateProductRequest("A", sku, 1), Ct)).Dispose();

        using var response = await client.PostAsJsonAsync(BaseUrl, new CreateProductRequest("B", sku.ToLowerInvariant(), 2), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Products.SkuAlreadyExists", (await ReadProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Update_ExistingProduct_PersistsChangesAndSetsUpdatedAt()
    {
        using var client = factory.CreateAuthenticatedClient();
        var id = await CreateAsync(client, NewSku());

        using var response = await client.PutAsJsonAsync($"{BaseUrl}/{id}", new UpdateProductRequest("Renamed", 99m), Ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var product = await client.GetFromJsonAsync<ProductResponse>($"{BaseUrl}/{id}", Ct);
        Assert.Equal("Renamed", product!.Name);
        Assert.Equal(99m, product.Price);
        Assert.NotNull(product.UpdatedAt);
    }

    [Fact]
    public async Task Update_UnknownId_Returns404()
    {
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}", new UpdateProductRequest("X", 1), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutAdminRole_Returns403()
    {
        using var client = factory.CreateAuthenticatedClient();
        var id = await CreateAsync(client, NewSku());

        using var response = await client.DeleteAsync($"{BaseUrl}/{id}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAdmin_SoftDeletesAndFreesSku()
    {
        using var admin = factory.CreateAuthenticatedClient("Admin");
        var sku = NewSku();
        var id = await CreateAsync(admin, sku);

        using var deleted = await admin.DeleteAsync($"{BaseUrl}/{id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var afterDelete = await admin.GetAsync($"{BaseUrl}/{id}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);

        // Index unique có điều kiện is_deleted = false → tạo lại cùng SKU được.
        using var recreated = await admin.PostAsJsonAsync(BaseUrl, new CreateProductRequest("Again", sku, 1), Ct);
        Assert.Equal(HttpStatusCode.Created, recreated.StatusCode);
    }

    [Fact]
    public async Task List_WithSearch_ReturnsOnlyMatchesWithPaging()
    {
        using var client = factory.CreateAuthenticatedClient();
        var tag = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 3; i++)
            await CreateAsync(client, $"{tag}-{i}");

        var page = await client.GetFromJsonAsync<PagedResult<ProductResponse>>($"{BaseUrl}?search={tag}&pageSize=2", Ct);

        Assert.NotNull(page);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(2, page.TotalPages);
        Assert.All(page.Items, p => Assert.StartsWith(tag.ToUpperInvariant(), p.Sku, StringComparison.Ordinal));
    }

    [Fact]
    public async Task List_SearchWithWildcardCharacter_IsTreatedLiterally()
    {
        using var client = factory.CreateAuthenticatedClient();

        var page = await client.GetFromJsonAsync<PagedResult<ProductResponse>>($"{BaseUrl}?search=%25", Ct);

        // "%" phải được escape, không khớp mọi bản ghi.
        Assert.Equal(0, page!.TotalCount);
    }

    [Fact]
    public async Task PriceStats_GroupsByRangeWithMedian()
    {
        using var client = factory.CreateAuthenticatedClient();
        // Chỉ test này tạo product giá >= 1000 → số liệu nhóm đó xác định được.
        foreach (var price in new[] { 5000m, 7000m, 9000m })
        {
            using var created = await client.PostAsJsonAsync(BaseUrl, new CreateProductRequest("Premium", NewSku(), price), Ct);
            created.EnsureSuccessStatusCode();
        }

        var stats = await client.GetFromJsonAsync<List<ProductPriceStatsResponse>>($"{BaseUrl}/price-stats", Ct);

        var premium = Assert.Single(stats!, s => s.PriceRange == ">= 1000");
        Assert.Equal(3, premium.ProductCount);
        Assert.Equal(5000m, premium.MinPrice);
        Assert.Equal(9000m, premium.MaxPrice);
        Assert.Equal(7000m, premium.AveragePrice);
        Assert.Equal(7000m, premium.MedianPrice);
    }

    private static string NewSku() => "T-" + Guid.NewGuid().ToString("N")[..12];

    private static async Task<Guid> CreateAsync(HttpClient client, string sku)
    {
        using var response = await client.PostAsJsonAsync(BaseUrl, new CreateProductRequest("Item " + sku, sku, 10), Ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(Ct);
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
}
