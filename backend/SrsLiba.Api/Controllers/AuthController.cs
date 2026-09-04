using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SrsLiba.Api.Data;
using SrsLiba.Api.Services;

namespace SrsLiba.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly TokenService _token;
    private readonly AppDbContext _db;
    public AuthController(TokenService token, AppDbContext db)
    {
        _token = token;
        _db = db;
    }

    public sealed record LoginRequest(string Username, string Password);
    public sealed record ConfirmRequest(string Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var result = await _token.LoginAsync(req.Username, req.Password, ct);
        if (result is null)
            return Unauthorized(new { message = "Username atau password salah." });

        return Ok(new
        {
            token = result.Token,
            username = req.Username,
            role = result.Role,
            isOperator = result.IsOperator,
            assignedCellId = result.AssignedCellId,
            assignedCellName = result.AssignedCellName,
            identityConfirmed = result.IdentityConfirmed
        });
    }

    /// <summary>
    /// Profil akun yang sedang login (dipakai frontend me-refresh status
    /// konfirmasi & cell operator setelah reload halaman).
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        if (username is null) return Unauthorized();
        var u = await _db.Users
            .Include(x => x.AssignedCell)
            .FirstOrDefaultAsync(x => x.Username.ToLower() == username.ToLower(), ct);
        if (u is null) return Unauthorized();

        return Ok(new
        {
            username = u.Username,
            role = u.Role,
            isOperator = u.IsOperator,
            assignedCellId = u.AssignedCellId,
            assignedCellName = u.AssignedCell?.CellName,
            identityConfirmed = u.IdentityConfirmed
        });
    }

    /// <summary>
    /// Konfirmasi identitas operator: verifikasi ulang password, lalu tandai
    /// IdentityConfirmed=true dan terbitkan token baru (claim terbarui).
    /// Hanya untuk akun operator milik sendiri.
    /// </summary>
    [Authorize]
    [HttpPost("confirm-identity")]
    public async Task<IActionResult> ConfirmIdentity([FromBody] ConfirmRequest req, CancellationToken ct)
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        if (username is null) return Unauthorized();
        var u = await _db.Users
            .Include(x => x.AssignedCell)
            .FirstOrDefaultAsync(x => x.Username.ToLower() == username.ToLower(), ct);
        if (u is null) return Unauthorized();
        if (!u.IsOperator)
            return BadRequest(new { message = "Akun ini bukan operator." });
        if (u.AssignedCellId is null)
            return BadRequest(new { message = "Operator belum ditetapkan ke cell mana pun. Hubungi admin." });
        if (!PasswordHasher.Verify(req.Password, u.PasswordHash))
            return Unauthorized(new { message = "Password salah. Konfirmasi identitas gagal." });

        u.IdentityConfirmed = true;
        u.ConfirmedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var token = _token.GenerateToken(u.Username, u.Role, u.IsOperator, u.AssignedCellId, u.IdentityConfirmed);
        return Ok(new
        {
            token,
            username = u.Username,
            role = u.Role,
            isOperator = u.IsOperator,
            assignedCellId = u.AssignedCellId,
            assignedCellName = u.AssignedCell?.CellName,
            identityConfirmed = u.IdentityConfirmed
        });
    }
}
