using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SrsLiba.Api.Services;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string Secret { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 60;
}

public sealed class TokenService
{
    private readonly JwtOptions _opt;
    private readonly List<(string Username, string Role, string PasswordHash)> _users;

    public TokenService(IOptions<JwtOptions> opt)
    {
        _opt = opt.Value;
        _users = new()
        {
            ("admin", "admin", PasswordHasher.Hash("admin")),
            ("user",  "user",  PasswordHasher.Hash("user"))
        };
    }

    public string? Authenticate(string username, string password)
    {
        var u = _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (u == default) return null;
        return PasswordHasher.Verify(password, u.PasswordHash) ? u.Role : null;
    }

    public string GenerateToken(string username, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role)
        };
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_opt.ExpiryMinutes),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string Token, string Role)? Login(string username, string password)
    {
        var role = Authenticate(username, password);
        return role is null ? null : (GenerateToken(username, role), role);
    }
}
