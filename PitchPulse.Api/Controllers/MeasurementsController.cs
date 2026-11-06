using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using PitchPulse.Api.Data;
using PitchPulse.Api.Models;

namespace PitchPulse.Api.Controllers;

[ApiController]
[Route("api/measurements")]
public class MeasurementsController : ControllerBase
{
    private readonly MeasurementStore _measurementStore;

    public MeasurementsController(MeasurementStore measurementStore)
    {
        _measurementStore = measurementStore;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<Measurement>> GetAll([FromQuery] string? series, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string sort = "timestamp", [FromQuery] int limit = 1000)
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

        return Ok(_measurementStore.GetAll(seriesIds, fromTime, toTime, sort == "-timestamp", limit));
    }

    [HttpGet("{id:int}")]
    public ActionResult<Measurement> GetById(int id)
    {
        var measurement = _measurementStore.GetById(id);

        if (measurement is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Nie znaleziono pomiaru.",
                detail: $"Pomiar o identyfikatorze {id} nie istnieje.");
        }

        return Ok(measurement);
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
