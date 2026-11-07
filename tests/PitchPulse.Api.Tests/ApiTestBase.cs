using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using PitchPulse.Api.Contracts;
using PitchPulse.Api.Data;
using PitchPulse.Api.Services;
using Xunit;

namespace PitchPulse.Api.Tests;

public abstract class ApiTestBase : IAsyncLifetime
{
    protected const string Username = "test-admin";
    protected const string Password = "Integration-Test-123!";
    private readonly string _databaseName = "pitchpulse_tests_" + Guid.NewGuid().ToString("N");
    private DbContextOptions<PitchPulseDbContext>? _dbOptions;
    private AppFactory? _factory;
    protected HttpClient _client = null!;
    protected static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

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

    protected PitchPulseDbContext OpenDatabase() => new(_dbOptions!);

    protected static IConfiguration AccountConfiguration(string password) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:Username"] = Username, ["Admin:Password"] = password }).Build();

    protected async Task<LoginResponse> LoginAsync(string password = Password)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/auth/login", new { username = Username, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: TestCancellation))!;
    }

    protected async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, string? token = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request, TestCancellation);
    }

    protected static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
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
