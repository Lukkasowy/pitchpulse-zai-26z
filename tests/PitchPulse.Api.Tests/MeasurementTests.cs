using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PitchPulse.Api.Contracts;
using PitchPulse.Api.Models;
using Xunit;

namespace PitchPulse.Api.Tests;

public class MeasurementTests : ApiTestBase
{
    [Fact]
    public async Task SensorWritesToItsOwnSeriesAndLosesAccessAfterRemoval()
    {
        var login = await LoginAsync();
        var sensor = await RegisterSensorAsync(login.AccessToken);

        var history = new DateTimeOffset(2026, 1, 6, 12, 0, 0, TimeSpan.FromHours(1));
        using var first = await SendMeasurementAsync(new { value = 40, timestamp = history.AddTicks(7), seriesId = 2 }, sensor.ApiKey);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.NotNull(first.Headers.Location);
        var saved = (await first.Content.ReadFromJsonAsync<Measurement>(cancellationToken: TestCancellation))!;
        Assert.Equal(1, saved.SeriesId);
        Assert.Equal(sensor.Id, saved.SensorId);
        Assert.Equal(40, saved.Value);
        Assert.Equal(history.ToUniversalTime(), saved.Timestamp);
        Assert.Equal(TimeSpan.Zero, saved.Timestamp.Offset);

        var started = DateTimeOffset.UtcNow.AddSeconds(-1);
        using var second = await SendMeasurementAsync(new { value = 220 }, sensor.ApiKey);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var current = (await second.Content.ReadFromJsonAsync<Measurement>(cancellationToken: TestCancellation))!;
        Assert.Equal(220, current.Value);
        Assert.InRange(current.Timestamp, started, DateTimeOffset.UtcNow);

        using var deletion = await SendAsync(HttpMethod.Delete, $"/api/sensors/{sensor.Id}", token: login.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        using var rejected = await SendMeasurementAsync(new { value = 140 }, sensor.ApiKey);
        await AssertProblemAsync(rejected, HttpStatusCode.Unauthorized);

        var retained = await _client.GetFromJsonAsync<Measurement>(first.Headers.Location, TestCancellation);
        Assert.NotNull(retained);
        Assert.Equal(saved.Id, retained.Id);
        Assert.Equal(saved.Value, retained.Value);
        Assert.Equal(saved.Timestamp, retained.Timestamp);
        Assert.Null(retained.SensorId);
    }

    [Fact]
    public async Task InvalidMeasurementsAreRejectedWithoutSaving()
    {
        var login = await LoginAsync();
        var sensor = await RegisterSensorAsync(login.AccessToken);

        using var adminOnly = await SendAsync(HttpMethod.Post, "/api/measurements", new { value = 140 }, login.AccessToken);
        await AssertProblemAsync(adminOnly, HttpStatusCode.Unauthorized);
        using var invalidKey = await SendMeasurementAsync(new { value = 140 }, new string('0', 64));
        await AssertProblemAsync(invalidKey, HttpStatusCode.Unauthorized);

        using var outOfRange = await SendMeasurementAsync(new { value = 221 }, sensor.ApiKey);
        await AssertProblemAsync(outOfRange, HttpStatusCode.UnprocessableEntity);
        using var future = await SendMeasurementAsync(new { value = 140, timestamp = DateTimeOffset.UtcNow.AddMinutes(6) }, sensor.ApiKey);
        await AssertProblemAsync(future, HttpStatusCode.UnprocessableEntity);

        var malformed = new object[] { new { }, new { value = "140" }, new { value = 140, timestamp = "2026-01-06T12:00:00" } };
        foreach (var body in malformed)
        {
            using var response = await SendMeasurementAsync(body, sensor.ApiKey);
            await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        }

        await using var db = OpenDatabase();
        Assert.False(await db.Measurements.AnyAsync(TestCancellation));
    }

    [Fact]
    public async Task ConcurrentWriteAndRangeChangeCannotBothSucceed()
    {
        var login = await LoginAsync();
        var sensor = await RegisterSensorAsync(login.AccessToken);

        var writeTask = SendMeasurementAsync(new { value = 190 }, sensor.ApiKey);
        var updateTask = SendAsync(HttpMethod.Put, "/api/series/1", new
        {
            name = "Tętno",
            minValue = 40,
            maxValue = 180,
            color = "#1f77b4"
        }, login.AccessToken);

        await Task.WhenAll(writeTask, updateTask);
        using var write = await writeTask;
        using var update = await updateTask;

        Assert.True(
            (write.StatusCode, update.StatusCode) is (HttpStatusCode.Created, HttpStatusCode.Conflict)
                or (HttpStatusCode.UnprocessableEntity, HttpStatusCode.OK),
            $"POST returned {write.StatusCode}, PUT returned {update.StatusCode}.");
    }

    private async Task<SensorCreatedResponse> RegisterSensorAsync(string token)
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/sensors", new { name = "Pulsometr", seriesId = 1 }, token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SensorCreatedResponse>(cancellationToken: TestCancellation))!;
    }

    private async Task<HttpResponseMessage> SendMeasurementAsync(object body, string apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/measurements") { Content = JsonContent.Create(body) };
        request.Headers.Add("X-API-Key", apiKey);
        return await _client.SendAsync(request, TestCancellation);
    }
}
