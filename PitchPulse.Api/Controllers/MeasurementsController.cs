using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PitchPulse.Api.Contracts;
using PitchPulse.Api.Data;
using PitchPulse.Api.Models;
using PitchPulse.Api.Security;

namespace PitchPulse.Api.Controllers;

[ApiController]
[Route("api/measurements")]
public class MeasurementsController : ControllerBase
{
    private readonly MeasurementStore _measurementStore;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MeasurementsController> _logger;

    public MeasurementsController(MeasurementStore measurementStore, TimeProvider timeProvider, ILogger<MeasurementsController> logger)
    {
        _measurementStore = measurementStore;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Measurement>>> GetAll([FromQuery] string? series, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string sort = "timestamp", [FromQuery] int limit = 1000, CancellationToken cancellationToken = default)
    {
        if (limit < 1 || limit > 10000)
        {
            return InvalidQuery("Limit musi być liczbą od 1 do 10000.");
        }

        if (sort != "timestamp" && sort != "-timestamp")
        {
            return InvalidQuery("Sortowanie musi mieć wartość timestamp lub -timestamp.");
        }

        List<int>? seriesIds = null;

        if (series is not null)
        {
            seriesIds = new List<int>();

            foreach (var part in series.Split(','))
            {
                if (!int.TryParse(part, out var id) || id < 1)
                {
                    return InvalidQuery("Podaj identyfikatory serii rozdzielone przecinkami, np. 1,3.");
                }

                seriesIds.Add(id);
            }
        }

        if (!TryParseTimestamp(from, out var fromTime) || !TryParseTimestamp(to, out var toTime))
        {
            return InvalidQuery("Daty from i to muszą być zapisane w ISO 8601 ze strefą, np. 2026-11-06T18:30:00Z.");
        }

        if (fromTime.HasValue && toTime.HasValue && fromTime.Value > toTime.Value)
        {
            return InvalidQuery("Początek przedziału nie może być późniejszy niż jego koniec.");
        }

        var measurements = await _measurementStore.GetAllAsync(seriesIds, fromTime, toTime, sort == "-timestamp", limit, cancellationToken);
        return Ok(measurements);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Measurement>> GetById(int id, CancellationToken cancellationToken)
    {
        var measurement = await _measurementStore.GetByIdAsync(id, cancellationToken);

        if (measurement is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Nie znaleziono pomiaru.",
                detail: $"Pomiar o identyfikatorze {id} nie istnieje.");
        }

        return Ok(measurement);
    }

    [HttpPost]
    [Authorize(AuthenticationSchemes = SensorAuthenticationHandler.SchemeName)]
    public async Task<ActionResult<Measurement>> Create(MeasurementRequest request, CancellationToken cancellationToken)
    {
        var seriesId = int.Parse(User.FindFirstValue(SensorAuthenticationHandler.SeriesIdClaim)!, CultureInfo.InvariantCulture);
        var sensorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        var now = _timeProvider.GetUtcNow();
        DateTimeOffset? timestamp = null;
        if (request.Timestamp.ValueKind != JsonValueKind.Undefined &&
            (request.Timestamp.ValueKind != JsonValueKind.String || !TryParseTimestamp(request.Timestamp.GetString(), out timestamp)))
        {
            _logger.LogWarning("Odrzucono pomiar serii {SeriesId}: niepoprawny znacznik czasu.", seriesId);
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Podaj znacznik czasu w formacie ISO 8601 ze strefą czasową.");
        }

        if (timestamp > now.AddMinutes(5))
        {
            _logger.LogWarning("Odrzucono pomiar serii {SeriesId}: znacznik czasu jest zbyt daleko w przyszłości.", seriesId);
            return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Znacznik czasu może wyprzedzać czas serwera najwyżej o 5 minut.");
        }

        var measurement = new Measurement { SeriesId = seriesId, Value = request.Value, Timestamp = timestamp ?? now };
        var result = await _measurementStore.CreateAsync(measurement, sensorId, cancellationToken);
        if (result == MeasurementWriteResult.RevokedSensor)
        {
            return Challenge(SensorAuthenticationHandler.SchemeName);
        }

        if (result == MeasurementWriteResult.InvalidValue)
        {
            _logger.LogWarning("Odrzucono pomiar serii {SeriesId}: wartość poza zakresem serii.", seriesId);
            return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Wartość pomiaru jest poza zakresem serii.");
        }

        return CreatedAtAction(nameof(GetById), new { id = measurement.Id }, measurement);
    }

    private ObjectResult InvalidQuery(string detail)
    {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Niepoprawne parametry zapytania.",
            detail: detail);
    }

    private static bool TryParseTimestamp(string? text, out DateTimeOffset? timestamp)
    {
        timestamp = null;

        if (text is null)
        {
            return true;
        }

        if (!Regex.IsMatch(text, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$") ||
            !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        timestamp = parsed.ToUniversalTime();
        return true;
    }
}
