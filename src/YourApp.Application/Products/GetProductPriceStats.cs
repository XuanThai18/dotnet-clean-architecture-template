using Dapper;
using MediatR;
using YourApp.Application.Abstractions;
using YourApp.Domain.Shared;

namespace YourApp.Application.Products;

public sealed record GetProductPriceStatsQuery : IRequest<Result<IReadOnlyList<ProductPriceStatsResponse>>>;

public sealed class ProductPriceStatsResponse
{
    public string PriceRange { get; init; } = string.Empty;
    public long ProductCount { get; init; }
    public decimal MinPrice { get; init; }
    public decimal MaxPrice { get; init; }
    public decimal AveragePrice { get; init; }
    public decimal MedianPrice { get; init; }
}

/// <summary>
/// Mẫu khi NÊN viết SQL tay (Dapper): báo cáo dùng GROUP BY + hàm thống kê riêng của Postgres
/// (percentile_cont) mà LINQ không dịch được. Dapper không qua query filter → tự lọc is_deleted.
/// </summary>
public sealed class GetProductPriceStatsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IRequestHandler<GetProductPriceStatsQuery, Result<IReadOnlyList<ProductPriceStatsResponse>>>
{
    private const string Sql = """
        SELECT
            CASE
                WHEN price < 100  THEN '< 100'
                WHEN price < 1000 THEN '100 - 999'
                ELSE '>= 1000'
            END                                                         AS price_range,
            count(*)                                                    AS product_count,
            min(price)                                                  AS min_price,
            max(price)                                                  AS max_price,
            round(avg(price), 2)                                        AS average_price,
            percentile_cont(0.5) WITHIN GROUP (ORDER BY price)::numeric(18, 2) AS median_price
        FROM products
        WHERE is_deleted = false
        GROUP BY 1
        ORDER BY min(price);
        """;

    public async Task<Result<IReadOnlyList<ProductPriceStatsResponse>>> Handle(
        GetProductPriceStatsQuery request, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ProductPriceStatsResponse>(
            new CommandDefinition(Sql, cancellationToken: cancellationToken));

        return rows.AsList();
    }
}
