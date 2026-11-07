using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PitchPulse.Api.Data;

namespace PitchPulse.Api.Security;

public class SensorAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    PitchPulseDbContext dbContext) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "SensorKey";
    public const string SeriesIdClaim = "series_id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-API-Key", out var values))
        {
            return AuthenticateResult.NoResult();
        }

        if (values.Count != 1 || values[0] is not { Length: 64 } apiKey)
        {
            return AuthenticateResult.Fail("Niepoprawny klucz czujnika.");
        }

        var hash = OpaqueTokens.Hash(apiKey);
        var sensor = await dbContext.Sensors.AsNoTracking()
            .Where(sensor => sensor.ApiKeyHash == hash)
            .Select(sensor => new { sensor.Id, sensor.SeriesId })
            .SingleOrDefaultAsync(Context.RequestAborted);

        if (sensor is null)
        {
            return AuthenticateResult.Fail("Niepoprawny klucz czujnika.");
        }

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, sensor.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(SeriesIdClaim, sensor.SeriesId.ToString(CultureInfo.InvariantCulture))
        }, SchemeName);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "ApiKey";
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Wymagany jest ważny klucz czujnika.",
            detail: "Podaj klucz czujnika w nagłówku X-API-Key.").ExecuteAsync(Context);
    }
}
