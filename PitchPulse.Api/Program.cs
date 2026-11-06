using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using PitchPulse.Api.Data;
using PitchPulse.Api.Security;
using PitchPulse.Api.Services;

var createAdmin = args.Contains("--create-admin");
var builder = WebApplication.CreateBuilder(args.Where(argument => argument != "--create-admin").ToArray());

builder.Services.AddControllers(options => options.ReturnHttpNotAcceptable = true);
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("PitchPulse")
    ?? throw new InvalidOperationException("Brak konfiguracji ConnectionStrings:PitchPulse.");

builder.Services.AddDbContext<PitchPulseDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<SeriesStore>();
builder.Services.AddScoped<MeasurementStore>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddAuthentication(AdminAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, AdminAuthenticationHandler>(AdminAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Zbyt wiele prób logowania. Spróbuj za minutę.")
            .ExecuteAsync(context.HttpContext);
    };
});

var app = builder.Build();

if (createAdmin)
{
    await using var scope = app.Services.CreateAsyncScope();
    await AdministratorSetup.CreateAsync(scope.ServiceProvider.GetRequiredService<PitchPulseDbContext>(), app.Configuration);
    return;
}

app.UseExceptionHandler(errorApp => errorApp.Run(context =>
    Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Wystąpił błąd serwera.").ExecuteAsync(context)));
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();

public partial class Program { }
