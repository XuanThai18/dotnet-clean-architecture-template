namespace YourApp.Api.Options;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>Rỗng = không cho gọi cross-origin.</summary>
    public string[] AllowedOrigins { get; init; } = [];
}
