using Microsoft.EntityFrameworkCore;
using PitchPulse.Api.Models;

namespace PitchPulse.Api.Data;

public class MeasurementStore
{
    private readonly PitchPulseDbContext _dbContext;

    public MeasurementStore(PitchPulseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Measurement>> GetAllAsync(IReadOnlyCollection<int>? seriesIds, DateTimeOffset? from, DateTimeOffset? to, bool descending, int limit, CancellationToken cancellationToken)
    {
        var query = _dbContext.Measurements.AsNoTracking();

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

        return await query.Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<Measurement?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _dbContext.Measurements.AsNoTracking()
            .FirstOrDefaultAsync(measurement => measurement.Id == id, cancellationToken);
    }
}
