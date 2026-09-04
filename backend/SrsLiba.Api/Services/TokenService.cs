using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SrsLiba.Api.Data;

namespace SrsLiba.Api.Services;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string Secret { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 60;
}

/// <summary>Hasil login: token + profil operator (untuk gating konfirmasi & scope cell).</summary>
public sealed record LoginResult(
    string Token,
    string Role,
    bool IsOperator,
    long? AssignedCellId,
    string? AssignedCellName,
    bool IdentityConfirmed);

/// <summary>
/// Autentikasi terhadap tabel AppUser lokal (scoped agar bisa memakai DbContext).
/// Token membawa claim operator: "assigned_cell", "is_operator", "identity_confirmed".
/// </summary>
public sealed class TokenService
{
    public const string AssignedCellClaim = "assigned_cell";
    public const string IsOperatorClaim = "is_operator";
    public const string IdentityConfirmedClaim = "identity_confirmed";

    private readonly JwtOptions _opt;
    private readonly AppDbContext _db;

    public TokenService(IOptions<JwtOptions> opt, AppDbContext db)
    {
        _opt = opt.Value;
        _db = db;
    }

    public async Task<LoginResult?> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var u = await _db.Users
            .Include(x => x.AssignedCell)
            .FirstOrDefaultAsync(x => x.Username.ToLower() == username.ToLower(), ct);
        if (u is null) return null;
        if (!PasswordHasher.Verify(password, u.PasswordHash)) return null;

        return new LoginResult(
            GenerateToken(u.Username, u.Role, u.IsOperator, u.AssignedCellId, u.IdentityConfirmed),
            u.Role,
            u.IsOperator,
            u.AssignedCellId,
            u.AssignedCell?.CellName,
            u.IdentityConfirmed);
    }

    public string GenerateToken(string username, string role, bool isOperator, long? assignedCellId, bool identityConfirmed)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim(IsOperatorClaim, isOperator ? "true" : "false"),
            new Claim(IdentityConfirmedClaim, identityConfirmed ? "true" : "false")
        };
        if (assignedCellId.HasValue)
            claims.Add(new Claim(AssignedCellClaim, assignedCellId.Value.ToString()));

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
}
