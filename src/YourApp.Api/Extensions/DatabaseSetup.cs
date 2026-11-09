using Microsoft.EntityFrameworkCore;
#if (AuthLocal)
using YourApp.Infrastructure.Auth;
#endif
using YourApp.Infrastructure.Persistence.Contexts;

namespace YourApp.Api.Extensions;

public static class DatabaseSetup
{
    /// <summary>
    /// Chỉ dùng cho local/dev (Database:MigrateOnStartup=true). Production chạy migration
    /// bằng bước riêng trong CI/CD: dotnet ef migrations bundle hoặc script idempotent.
    /// </summary>
    public static async Task ApplyMigrationsIfEnabledAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
            return;

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
#if (AuthLocal)
        await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
#endif
    }
}
