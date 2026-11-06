using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PitchPulse.Api.Contracts;
using PitchPulse.Api.Security;
using PitchPulse.Api.Services;

namespace PitchPulse.Api.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(request.Username, request.Password, cancellationToken);
        if (response is null)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Niepoprawna nazwa użytkownika lub hasło.");
        }

        return Ok(response);
    }

    [HttpPost("logout")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var administratorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var sessionId = int.Parse(User.FindFirstValue(AdminAuthenticationHandler.SessionIdClaim)!);
        var removed = await _authService.LogoutAsync(administratorId, sessionId, cancellationToken);
        return removed ? NoContent() : Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sesja została unieważniona.");
    }

    [HttpPut("password")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> ChangePassword(PasswordChangeRequest request, CancellationToken cancellationToken)
    {
        if (request.NewPassword.Length < 8 || Encoding.UTF8.GetByteCount(request.NewPassword) > 72)
        {
            return Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Niepoprawne nowe hasło.",
                detail: "Hasło musi mieć co najmniej 8 znaków i najwyżej 72 bajty UTF-8.");
        }

        var administratorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var sessionId = int.Parse(User.FindFirstValue(AdminAuthenticationHandler.SessionIdClaim)!);
        var result = await _authService.ChangePasswordAsync(administratorId, sessionId, request.CurrentPassword, request.NewPassword, cancellationToken);

        return result switch
        {
            PasswordChangeResult.Success => NoContent(),
            PasswordChangeResult.InvalidSession => Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sesja została unieważniona."),
            _ => Problem(statusCode: StatusCodes.Status403Forbidden, title: "Bieżące hasło jest niepoprawne.")
        };
    }
}
