using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PitchPulse.Api.Data;

namespace PitchPulse.Api.Security;

public class AdminAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "AdminSession";
    public const string SessionIdClaim = "session_id";
    private readonly PitchPulseDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public AdminAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        PitchPulseDbContext dbContext,
        TimeProvider timeProvider) : base(options, logger, encoder)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return AuthenticateResult.NoResult();
        }

        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization.ToString(), out var header) ||
            !header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) ||
            header.Parameter is not { Length: 64 } token)
        {
            return AuthenticateResult.Fail("Niepoprawny token.");
        }

        var tokenHash = SessionTokens.Hash(token);
        var now = _timeProvider.GetUtcNow();
        var session = await _dbContext.AdminSessions.AsNoTracking()
            .SingleOrDefaultAsync(session => session.TokenHash == tokenHash && session.ExpiresAt > now, Context.RequestAborted);

        if (session is null)
        {
            return AuthenticateResult.Fail("Sesja wygasła lub została unieważniona.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, session.AdministratorId.ToString()),
            new Claim(ClaimTypes.Role, "Administrator"),
            new Claim(SessionIdClaim, session.Id.ToString())
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        Response.Headers.CacheControl = "no-store";
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Wymagane logowanie.",
            detail: "Podaj ważny token administratora w nagłówku Authorization: Bearer.").ExecuteAsync(Context);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Brak uprawnień.").ExecuteAsync(Context);
    }
}
