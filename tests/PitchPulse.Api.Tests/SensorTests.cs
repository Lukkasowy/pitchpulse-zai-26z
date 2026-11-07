using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PitchPulse.Api.Contracts;
using PitchPulse.Api.Models;
using Xunit;

namespace PitchPulse.Api.Tests;

public class SensorTests : ApiTestBase
{
    [Fact]
    public async Task RegistrationReturnsKeyOnceAndStoresItsHash()
    {
        var login = await LoginAsync();
        using var registration = await SendAsync(HttpMethod.Post, "/api/sensors", new { name = "Pulsometr", seriesId = 1 }, login.AccessToken);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        Assert.True(registration.Headers.CacheControl?.NoStore);
        Assert.NotNull(registration.Headers.Location);
        var sensor = (await registration.Content.ReadFromJsonAsync<SensorCreatedResponse>(cancellationToken: TestCancellation))!;
        Assert.Equal(1, sensor.SeriesId);
        Assert.Equal("Pulsometr", sensor.Name);
        Assert.True(sensor.ApiKey.Length >= 32);

        await using var db = OpenDatabase();
        var saved = await db.Sensors.SingleAsync(TestCancellation);
        Assert.Equal(sensor.Id, saved.Id);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sensor.ApiKey))), saved.ApiKeyHash);

        foreach (var path in new[] { "/api/sensors", registration.Headers.Location.PathAndQuery })
        {
            using var response = await SendAsync(HttpMethod.Get, path, token: login.AccessToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestCancellation));
            var item = json.RootElement.ValueKind == JsonValueKind.Array ? json.RootElement[0] : json.RootElement;
            Assert.Equal(sensor.Id, item.GetProperty("id").GetInt32());
            Assert.False(item.TryGetProperty("apiKey", out _));
            Assert.False(item.TryGetProperty("apiKeyHash", out _));
        }
    }

    [Fact]
    public async Task SensorEndpointsRequireAdminAndRejectInvalidRegistration()
    {
        var input = new { name = "Pulsometr", seriesId = 1 };
        await AssertProblemAsync(await SendAsync(HttpMethod.Get, "/api/sensors"), HttpStatusCode.Unauthorized);
        await AssertProblemAsync(await SendAsync(HttpMethod.Get, "/api/sensors/1"), HttpStatusCode.Unauthorized);
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/sensors", input), HttpStatusCode.Unauthorized);
        await AssertProblemAsync(await SendAsync(HttpMethod.Delete, "/api/sensors/1"), HttpStatusCode.Unauthorized);

        var login = await LoginAsync();
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/sensors", new { name = "Pulsometr" }, login.AccessToken), HttpStatusCode.BadRequest);
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/sensors", new { name = " ", seriesId = 1 }, login.AccessToken), HttpStatusCode.BadRequest);
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/sensors", new { name = "Pulsometr", seriesId = 999999 }, login.AccessToken), HttpStatusCode.UnprocessableEntity);
        await AssertProblemAsync(await SendAsync(HttpMethod.Get, "/api/sensors/999999", token: login.AccessToken), HttpStatusCode.NotFound);
        await AssertProblemAsync(await SendAsync(HttpMethod.Delete, "/api/sensors/999999", token: login.AccessToken), HttpStatusCode.NotFound);
        await using var db = OpenDatabase();
        Assert.False(await db.Sensors.AnyAsync(TestCancellation));
    }

    [Fact]
    public async Task RemovingASensorKeepsItsMeasurementAndRemovingTheSeriesDeletesBoth()
    {
        var login = await LoginAsync();
        using var firstRegistration = await SendAsync(HttpMethod.Post, "/api/sensors", new { name = "Pulsometr A", seriesId = 1 }, login.AccessToken);
        using var secondRegistration = await SendAsync(HttpMethod.Post, "/api/sensors", new { name = "Pulsometr B", seriesId = 1 }, login.AccessToken);
        Assert.Equal(HttpStatusCode.Created, firstRegistration.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondRegistration.StatusCode);
        var first = (await firstRegistration.Content.ReadFromJsonAsync<SensorCreatedResponse>(cancellationToken: TestCancellation))!;
        var second = (await secondRegistration.Content.ReadFromJsonAsync<SensorCreatedResponse>(cancellationToken: TestCancellation))!;
        Assert.NotEqual(first.ApiKey, second.ApiKey);

        await using var db = OpenDatabase();
        var measurement = new Measurement { SeriesId = 1, SensorId = first.Id, Value = 150, Timestamp = DateTimeOffset.UtcNow };
        var secondMeasurement = new Measurement { SeriesId = 1, SensorId = second.Id, Value = 155, Timestamp = measurement.Timestamp };
        db.Measurements.AddRange(measurement, secondMeasurement);
        await db.SaveChangesAsync(TestCancellation);

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, $"/api/sensors/{first.Id}", token: login.AccessToken)).StatusCode);
        await AssertProblemAsync(await SendAsync(HttpMethod.Get, $"/api/sensors/{first.Id}", token: login.AccessToken), HttpStatusCode.NotFound);
        using var retained = await _client.GetAsync($"/api/measurements/{measurement.Id}", TestCancellation);
        Assert.Equal(HttpStatusCode.OK, retained.StatusCode);
        using var retainedJson = JsonDocument.Parse(await retained.Content.ReadAsStringAsync(TestCancellation));
        Assert.False(retainedJson.RootElement.TryGetProperty("sensorId", out _));
        Assert.Equal(150, retainedJson.RootElement.GetProperty("value").GetDouble());

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, "/api/series/1", token: login.AccessToken)).StatusCode);
        await AssertProblemAsync(await SendAsync(HttpMethod.Get, $"/api/sensors/{second.Id}", token: login.AccessToken), HttpStatusCode.NotFound);
        Assert.False(await db.Measurements.AnyAsync(item => item.SeriesId == 1, TestCancellation));
    }
}
