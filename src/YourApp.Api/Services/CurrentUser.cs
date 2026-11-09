using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using YourApp.Application.Abstractions;

namespace YourApp.Api.Services;

internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public string? UserId => Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
