using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PitchPulse.Api.Contracts;
using PitchPulse.Api.Models;
using Xunit;

namespace PitchPulse.Api.Tests;

public class SeriesTests : ApiTestBase
{
    [Fact]
    public async Task CreatedSeriesCanBeReadPubliclyAtItsLocation()
    {
        var login = await LoginAsync();
        var response = await SendAsync(HttpMethod.Post, "/api/series", new
        {
            name = "Prędkość zawodnika nr 7",
            minValue = 0,
            maxValue = 40,
            color = "#1565c0",
            icon = "circle",
            unit = "km/h",
            playerId = 1,
            metricType = "Speed"
        }, login.AccessToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var series = (await response.Content.ReadFromJsonAsync<SeriesResponse>(cancellationToken: TestCancellation))!;
        var read = await _client.GetAsync(response.Headers.Location, TestCancellation);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var saved = (await read.Content.ReadFromJsonAsync<SeriesResponse>(cancellationToken: TestCancellation))!;
        Assert.Equal(series.Id, saved.Id);
        Assert.Equal("Prędkość zawodnika nr 7", saved.Name);
        Assert.Equal("Speed", saved.MetricType);
        Assert.Equal(1, saved.PlayerId);
        Assert.Equal("km/h", saved.Unit);
        await using var db = OpenDatabase();
        Assert.True(await db.Series.AnyAsync(item => item.Id == series.Id && item.MetricType == MetricType.Speed, TestCancellation));
    }

    [Fact]
    public async Task PutReplacesAllEditableFieldsAndClearsOmittedOptionalFields()
    {
        var login = await LoginAsync();
        var response = await SendAsync(HttpMethod.Put, "/api/series/1", new
        {
            name = "Nowa nazwa",
            minValue = -10,
            maxValue = 50,
            color = "#ABCDEF"
        }, login.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var series = (await response.Content.ReadFromJsonAsync<SeriesResponse>(cancellationToken: TestCancellation))!;
        Assert.Equal(1, series.Id);
        Assert.Equal("Nowa nazwa", series.Name);
        Assert.Equal(-10, series.MinValue);
        Assert.Equal(50, series.MaxValue);
        Assert.Equal("#ABCDEF", series.Color);
        Assert.Null(series.Icon);
        Assert.Null(series.Unit);
        Assert.Null(series.PlayerId);
        Assert.Equal("Custom", series.MetricType);
        await using var db = OpenDatabase();
        Assert.Equal("Nowa nazwa", (await db.Series.SingleAsync(item => item.Id == 1, TestCancellation)).Name);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(20, 10)]
    [InlineData(-10, -20)]
    public async Task InvalidRangesReturn422WithoutSaving(double minValue, double maxValue)
    {
        var login = await LoginAsync();
        var input = new { name = "Invalid range", minValue, maxValue, color = "#123456" };
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/series", input, login.AccessToken), HttpStatusCode.UnprocessableEntity);
        await AssertProblemAsync(await SendAsync(HttpMethod.Put, "/api/series/1", input, login.AccessToken), HttpStatusCode.UnprocessableEntity);
        await using var db = OpenDatabase();
        Assert.Equal(3, await db.Series.CountAsync(TestCancellation));
        Assert.Equal(40, (await db.Series.SingleAsync(item => item.Id == 1, TestCancellation)).MinValue);
    }

    [Fact]
    public async Task InvalidFieldsAndMissingRequiredNumbersReturn400()
    {
        var login = await LoginAsync();
        object[] inputs =
        [
            new { name = "Missing minimum", maxValue = 100, color = "#123456" },
            new { name = "Missing maximum", minValue = 0, color = "#123456" },
            new { name = "", minValue = 0, maxValue = 100, color = "#123456" },
            new { name = new string('a', 101), minValue = 0, maxValue = 100, color = "#123456" },
            new { name = "Invalid color", minValue = 0, maxValue = 100, color = "blue" },
            new { name = "Color with newline", minValue = 0, maxValue = 100, color = "#123456\n" },
            new { name = "Invalid unit", minValue = 0, maxValue = 100, color = "#123456", unit = new string('a', 21) },
            new { name = "Invalid type", minValue = 0, maxValue = 100, color = "#123456", metricType = "Unknown" }
        ];
        foreach (var input in inputs)
        {
            await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/series", input, login.AccessToken), HttpStatusCode.BadRequest);
        }

        using var malformed = new HttpRequestMessage(HttpMethod.Post, "/api/series")
        {
            Content = new StringContent("{", Encoding.UTF8, "application/json")
        };
        malformed.Headers.Authorization = new("Bearer", login.AccessToken);
        await AssertProblemAsync(await _client.SendAsync(malformed, TestCancellation), HttpStatusCode.BadRequest);
        await using var db = OpenDatabase();
        Assert.Equal(3, await db.Series.CountAsync(TestCancellation));
    }

    [Fact]
    public async Task MutationsRequireAnActiveAdministratorSession()
    {
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/series", Input()), HttpStatusCode.Unauthorized);
        await AssertProblemAsync(await SendAsync(HttpMethod.Put, "/api/series/1", Input()), HttpStatusCode.Unauthorized);
        await AssertProblemAsync(await SendAsync(HttpMethod.Delete, "/api/series/1"), HttpStatusCode.Unauthorized);
        var login = await LoginAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post, "/api/auth/logout", token: login.AccessToken)).StatusCode);
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/series", Input(), login.AccessToken), HttpStatusCode.Unauthorized);
        await AssertProblemAsync(await SendAsync(HttpMethod.Put, "/api/series/1", Input(), login.AccessToken), HttpStatusCode.Unauthorized);
        await AssertProblemAsync(await SendAsync(HttpMethod.Delete, "/api/series/1", token: login.AccessToken), HttpStatusCode.Unauthorized);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/series", TestCancellation)).StatusCode);
    }

    [Fact]
    public async Task MissingSeriesReturn404ForReadUpdateAndDelete()
    {
        var login = await LoginAsync();
        await AssertProblemAsync(await _client.GetAsync("/api/series/999999", TestCancellation), HttpStatusCode.NotFound);
        await AssertProblemAsync(await SendAsync(HttpMethod.Put, "/api/series/999999", Input(), login.AccessToken), HttpStatusCode.NotFound);
        await AssertProblemAsync(await SendAsync(HttpMethod.Delete, "/api/series/999999", token: login.AccessToken), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RangeConflictDoesNotPartiallyUpdateSeriesAndBoundariesAreInclusive()
    {
        var login = await LoginAsync();
        var series = await CreateSeriesAsync(login.AccessToken);
        await using var db = OpenDatabase();
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-1);
        db.Measurements.AddRange(
            new Measurement { SeriesId = series.Id, Value = 20, Timestamp = timestamp },
            new Measurement { SeriesId = series.Id, Value = 80, Timestamp = timestamp });
        await db.SaveChangesAsync(TestCancellation);

        var conflict = await SendAsync(HttpMethod.Put, $"/api/series/{series.Id}", new { name = "Rejected name", minValue = 30, maxValue = 90, color = "#abcdef" }, login.AccessToken);
        await AssertProblemAsync(conflict, HttpStatusCode.Conflict);
        var unchanged = await db.Series.AsNoTracking().SingleAsync(item => item.Id == series.Id, TestCancellation);
        Assert.Equal(series.Name, unchanged.Name);
        Assert.Equal(series.Color, unchanged.Color);
        Assert.Equal(0, unchanged.MinValue);
        Assert.Equal(100, unchanged.MaxValue);

        var accepted = await SendAsync(HttpMethod.Put, $"/api/series/{series.Id}", new { name = "Accepted range", minValue = 20, maxValue = 80, color = "#abcdef" }, login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(2, await db.Measurements.CountAsync(measurement => measurement.SeriesId == series.Id, TestCancellation));
    }

    [Fact]
    public async Task DeletingSeriesRemovesOnlyItsMeasurementsAndKeepsPlayers()
    {
        var login = await LoginAsync();
        await using var db = OpenDatabase();
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-1);
        var removedMeasurement = new Measurement { SeriesId = 1, Value = 150, Timestamp = timestamp };
        db.Measurements.AddRange(removedMeasurement, new Measurement { SeriesId = 2, Value = 155, Timestamp = timestamp });
        await db.SaveChangesAsync(TestCancellation);

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, "/api/series/1", token: login.AccessToken)).StatusCode);
        await AssertProblemAsync(await _client.GetAsync("/api/series/1", TestCancellation), HttpStatusCode.NotFound);
        await AssertProblemAsync(await _client.GetAsync($"/api/measurements/{removedMeasurement.Id}", TestCancellation), HttpStatusCode.NotFound);
        Assert.False(await db.Measurements.AnyAsync(measurement => measurement.SeriesId == 1, TestCancellation));
        Assert.Equal(1, await db.Measurements.CountAsync(measurement => measurement.SeriesId == 2, TestCancellation));
        Assert.True(await db.Players.AnyAsync(player => player.Id == 1, TestCancellation));
        await AssertProblemAsync(await SendAsync(HttpMethod.Delete, "/api/series/1", token: login.AccessToken), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnknownPlayerReturns422WithoutChangingData()
    {
        var login = await LoginAsync();
        var input = new { name = "Unknown player", minValue = 0, maxValue = 100, color = "#123456", playerId = 999999 };
        await AssertProblemAsync(await SendAsync(HttpMethod.Post, "/api/series", input, login.AccessToken), HttpStatusCode.UnprocessableEntity);
        await AssertProblemAsync(await SendAsync(HttpMethod.Put, "/api/series/1", input, login.AccessToken), HttpStatusCode.UnprocessableEntity);
        await using var db = OpenDatabase();
        Assert.Equal(3, await db.Series.CountAsync(TestCancellation));
        Assert.Equal(1, (await db.Series.SingleAsync(item => item.Id == 1, TestCancellation)).PlayerId);
    }

    [Fact]
    public async Task GenericSeriesCanUseNegativeRangesAndAnyUnit()
    {
        var login = await LoginAsync();
        var response = await SendAsync(HttpMethod.Post, "/api/series", new { name = "Temperature", minValue = -30.5, maxValue = -10.5, color = "#abcdef", unit = "°C" }, login.AccessToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var series = (await response.Content.ReadFromJsonAsync<SeriesResponse>(cancellationToken: TestCancellation))!;
        Assert.Equal(-30.5, series.MinValue);
        Assert.Null(series.PlayerId);
        Assert.Equal("Custom", series.MetricType);
        Assert.Equal("°C", series.Unit);
    }

    private static object Input() => new { name = "Test series", minValue = 0, maxValue = 100, color = "#123456" };

    private async Task<SeriesResponse> CreateSeriesAsync(string token)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/series", Input(), token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SeriesResponse>(cancellationToken: TestCancellation))!;
    }
}
