using Microsoft.AspNetCore.Mvc;
using SrsLiba.Api.Services;

namespace SrsLiba.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly TokenService _token;
    public AuthController(TokenService token) => _token = token;

    public sealed record LoginRequest(string Username, string Password);

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        var result = _token.Login(req.Username, req.Password);
        if (result is null)
            return Unauthorized(new { message = "Username atau password salah." });

        return Ok(new { token = result.Value.Token, username = req.Username, role = result.Value.Role });
    }
}
