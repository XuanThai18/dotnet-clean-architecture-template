using System.Data.Common;
using Npgsql;
using YourApp.Application.Abstractions;

namespace YourApp.Infrastructure.Persistence;

internal sealed class SqlConnectionFactory(NpgsqlDataSource dataSource) : ISqlConnectionFactory
{
    public async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default) =>
        await dataSource.OpenConnectionAsync(cancellationToken);
}
