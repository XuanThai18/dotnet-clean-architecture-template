using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using YourApp.Api.Options;

namespace YourApp.Api.Services;

/// <summary>
/// Ký JWT bằng cùng khoá mà API dùng để kiểm. Template chưa có màn đăng nhập:
/// khi thêm (Identity, Keycloak...), endpoint login gọi lớp này hoặc thay bằng IdP bên ngoài.
/// </summary>
public sealed class JwtTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    public string Issue(string subject, IEnumerable<string> roles, TimeSpan lifetime)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, subject) };
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(lifetime),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)), SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
