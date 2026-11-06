using PitchPulse.Api.Models;

namespace PitchPulse.Api.Data;

public class MeasurementStore
{
    private readonly List<Measurement> _measurements = new();

    public IReadOnlyList<Measurement> GetAll(IReadOnlyCollection<int>? seriesIds, DateTimeOffset? from, DateTimeOffset? to, bool descending, int limit)
    {
        var query = _measurements.AsEnumerable();

        if (seriesIds is not null)
        {
            query = query.Where(measurement => seriesIds.Contains(measurement.SeriesId));
        }

        if (from.HasValue)
        {
            query = query.Where(measurement => measurement.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(measurement => measurement.Timestamp <= to.Value);
        }

        query = descending
            ? query.OrderByDescending(measurement => measurement.Timestamp).ThenByDescending(measurement => measurement.Id)
            : query.OrderBy(measurement => measurement.Timestamp).ThenBy(measurement => measurement.Id);

        return query.Take(limit).ToList();
    }

    public Measurement? GetById(int id)
    {
        return _measurements.FirstOrDefault(measurement => measurement.Id == id);
    }
}
