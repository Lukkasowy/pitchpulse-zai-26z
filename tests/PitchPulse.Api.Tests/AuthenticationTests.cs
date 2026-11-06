using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using PitchPulse.Api.Contracts;
using PitchPulse.Api.Data;
using PitchPulse.Api.Security;
using PitchPulse.Api.Services;
using Xunit;

namespace PitchPulse.Api.Tests;

public class AuthenticationTests : IAsyncLifetime
{
    private const string Username = "test-admin";
    private const string Password = "Integration-Test-123!";
    private readonly string _databaseName = "pitchpulse_tests_" + Guid.NewGuid().ToString("N");
    private DbContextOptions<PitchPulseDbContext>? _dbOptions;
    private AppFactory? _factory;
    private HttpClient _client = null!;
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationBuilder().AddUserSecrets(typeof(PitchPulseDbContext).Assembly).Build();
        var connectionString = Environment.GetEnvironmentVariable("PITCHPULSE_TEST_CONNECTION")
            ?? configuration.GetConnectionString("PitchPulse")
            ?? throw new InvalidOperationException("Ustaw PITCHPULSE_TEST_CONNECTION lub lokalne połączenie PitchPulse w User Secrets.");

        var connection = new NpgsqlConnectionStringBuilder(connectionString) { Database = _databaseName };
        _dbOptions = new DbContextOptionsBuilder<PitchPulseDbContext>().UseNpgsql(connection.ConnectionString).Options;
        await using var db = OpenDatabase();
        await db.Database.MigrateAsync();
        await AdministratorSetup.CreateAsync(db, AccountConfiguration(Password));

        _factory = new AppFactory(connection.ConnectionString);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_dbOptions is not null && _databaseName.StartsWith("pitchpulse_tests_", StringComparison.Ordinal))
        {
            await using var db = OpenDatabase();
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task LoginStoresOnlyHashesAndReturnsAnExpiringToken()
    {
        var response = await SendAsync(HttpMethod.Post, "/api/auth/login", new { username = Username, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var login = (await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: TestCancellation))!;
        Assert.Equal("Bearer", login.TokenType);
        Assert.Equal(3600, login.ExpiresIn);
        Assert.Equal(64, login.AccessToken.Length);

        await using var db = OpenDatabase();
        var session = await db.AdminSessions.SingleAsync(TestCancellation);
        Assert.NotEqual(login.AccessToken, session.TokenHash);
        Assert.Equal(SessionTokens.Hash(login.AccessToken), session.TokenHash);
        Assert.Equal(TimeSpan.FromHours(1), session.ExpiresAt - session.CreatedAt);
        var administrator = await db.Administrators.SingleAsync(TestCancellation);
        Assert.NotEqual(Password, administrator.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(Password, administrator.PasswordHash));
        var json = await response.Content.ReadAsStringAsync(TestCancellation);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tokenHash", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("test-admin", "WrongPassword-123!")]
    [InlineData("unknown-admin", "Integration-Test-123!")]
    [InlineData("' OR 1=1 --", "Integration-Test-123!")]
    public async Task InvalidCredentialsDoNotCreateASession(string username, string password)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/auth/login", new { username, password });
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
        await using var db = OpenDatabase();
        Assert.False(await db.AdminSessions.AnyAsync(TestCancellation));
    }

    [Fact]
    public async Task LogoutRevokesOnlyTheCurrentSession()
    {
        var first = await LoginAsync();
        var second = await LoginAsync();
        Assert.NotEqual(first.AccessToken, second.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post, "/api/auth/logout", token: first.AccessToken)).StatusCode);
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/auth/logout", token: first.AccessToken), HttpStatusCode.Unauthorized);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post, "/api/auth/logout", token: second.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task PasswordChangeRevokesAllSessionsAndRejectsTheOldPassword()
    {
        var first = await LoginAsync();
        var second = await LoginAsync();
        const string newPassword = "Changed-Test-Password-456!";
        var changed = await SendAsync(HttpMethod.Put, "/api/auth/password", new { currentPassword = Password, newPassword }, first.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/auth/logout", token: first.AccessToken), HttpStatusCode.Unauthorized);
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/auth/logout", token: second.AccessToken), HttpStatusCode.Unauthorized);
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/auth/login", new { username = Username, password = Password }), HttpStatusCode.Unauthorized);
        Assert.NotNull(await LoginAsync(newPassword));
    }

    [Fact]
    public async Task WrongCurrentPasswordDoesNotChangePasswordOrRevokeTheSession()
    {
        var login = await LoginAsync();
        var response = await SendAsync(HttpMethod.Put, "/api/auth/password", new { currentPassword = "Wrong-Password", newPassword = "New-Password-123!" }, login.AccessToken);
        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
        Assert.NotNull(await LoginAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post, "/api/auth/logout", token: login.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task PasswordValidationChecksLengthAndUtf8Bytes()
    {
        var login = await LoginAsync();
        foreach (var newPassword in new[] { "short", new string('a', 73), new string('ą', 37) })
        {
            var response = await SendAsync(HttpMethod.Put, "/api/auth/password", new { currentPassword = Password, newPassword }, login.AccessToken);
            await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity);
        }

        var validPassword = new string('ą', 36);
        var changed = await SendAsync(HttpMethod.Put, "/api/auth/password", new { currentPassword = Password, newPassword = validPassword }, login.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.NotNull(await LoginAsync(validPassword));
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/auth/login", new { username = Username, password = validPassword + "x" }), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredTokensCannotBeUsedAndAreCleanedUpAtLogin()
    {
        var login = await LoginAsync();
        await using var db = OpenDatabase();
        await db.AdminSessions.ExecuteUpdateAsync(update => update.SetProperty(session => session.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)), TestCancellation);
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/auth/logout", token: login.AccessToken), HttpStatusCode.Unauthorized);
        var nextLogin = await LoginAsync();
        var sessions = await db.AdminSessions.AsNoTracking().ToListAsync(TestCancellation);
        Assert.Single(sessions);
        Assert.Equal(SessionTokens.Hash(nextLogin.AccessToken), sessions[0].TokenHash);
    }

    [Fact]
    public async Task ProtectedEndpointsRequireBearerAndPublicReadsStayPublic()
    {
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/auth/logout"), HttpStatusCode.Unauthorized);
        await AssertProblemAsync(await SendAsync(HttpMethod.Put, "/api/auth/password", new { currentPassword = Password, newPassword = "New-Password-123!" }), HttpStatusCode.Unauthorized);
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/auth/logout", token: new string('A', 64)), HttpStatusCode.Unauthorized);
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/auth/logout", token: "malformed"), HttpStatusCode.Unauthorized);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/series", TestCancellation)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/measurements", TestCancellation)).StatusCode);
    }

    [Fact]
    public async Task MalformedLoginRequestsReturnProblemDetails()
    {
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/auth/login", new { username = Username }), HttpStatusCode.BadRequest);
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/auth/login", new { username = Username, password = 123 }), HttpStatusCode.BadRequest);
        using var malformed = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = new StringContent("{", Encoding.UTF8, "application/json") };
        await AssertProblemAsync(await _client.SendAsync(malformed, TestCancellation), HttpStatusCode.BadRequest);
        using var plain = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = new StringContent("username=test", Encoding.UTF8, "text/plain") };
        await AssertProblemAsync(await _client.SendAsync(plain, TestCancellation), HttpStatusCode.UnsupportedMediaType);
        var login = await LoginAsync();
        await AssertProblemAsync(await SendAsync(HttpMethod.Put, "/api/auth/password", new { currentPassword = Password }, login.AccessToken), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AccountSetupDoesNotResetAnExistingPassword()
    {
        await using var db = OpenDatabase();
        await AdministratorSetup.CreateAsync(db, AccountConfiguration("Different-Initial-Password"));
        Assert.Equal(1, await db.Administrators.CountAsync(TestCancellation));
        Assert.NotNull(await LoginAsync());
    }

    [Fact]
    public async Task ConcurrentLoginsCannotLeaveOldPasswordSessionsAfterPasswordChange()
    {
        var login = await LoginAsync();
        var logins = Enumerable.Range(0, 5).Select(_ => SendAsync(HttpMethod.Post, "/api/auth/login", new { username = Username, password = Password })).ToArray();
        var changed = await SendAsync(HttpMethod.Put, "/api/auth/password", new { currentPassword = Password, newPassword = "Concurrent-New-Password!" }, login.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        foreach (var response in await Task.WhenAll(logins))
        {
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized });
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var oldLogin = (await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: TestCancellation))!;
                await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/auth/logout", token: oldLogin.AccessToken), HttpStatusCode.Unauthorized);
            }
        }

        await using var db = OpenDatabase();
        Assert.False(await db.AdminSessions.AnyAsync(TestCancellation));
        Assert.NotNull(await LoginAsync("Concurrent-New-Password!"));
    }

    [Fact]
    public async Task LoginRateLimitReturns429WithRetryAfter()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Post, "/api/auth/login", new { username = Username, password = "Wrong-Password" })).StatusCode);
        }

        var limited = await SendAsync(HttpMethod.Post, "/api/auth/login", new { username = Username, password = Password });
        await AssertProblemAsync(limited, HttpStatusCode.TooManyRequests);
        Assert.NotNull(limited.Headers.RetryAfter);
    }

    private PitchPulseDbContext OpenDatabase() => new(_dbOptions!);

    private static IConfiguration AccountConfiguration(string password) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:Username"] = Username, ["Admin:Password"] = password }).Build();

    private async Task<LoginResponse> LoginAsync(string password = Password)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/auth/login", new { username = Username, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: TestCancellation))!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, string? token = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request, TestCancellation);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestCancellation));
        Assert.Equal((int)status, problem.RootElement.GetProperty("status").GetInt32());
        Assert.True(problem.RootElement.TryGetProperty("title", out _));
    }

    private sealed class AppFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:PitchPulse", connectionString);
            builder.ConfigureLogging(logging => logging.ClearProviders());
        }
    }
}
