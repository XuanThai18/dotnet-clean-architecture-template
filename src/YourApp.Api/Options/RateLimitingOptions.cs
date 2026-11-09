using System.ComponentModel.DataAnnotations;

namespace YourApp.Api.Options;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Range(1, int.MaxValue)]
    public int PermitLimit { get; init; } = 100;

    [Range(1, 3600)]
    public int WindowSeconds { get; init; } = 60;

    /// <summary>Số request mỗi cửa sổ, theo IP, cho endpoint nhạy cảm (policy "sensitive": đăng nhập, refresh...).</summary>
    [Range(1, int.MaxValue)]
    public int SensitivePermitLimit { get; init; } = 10;
}
