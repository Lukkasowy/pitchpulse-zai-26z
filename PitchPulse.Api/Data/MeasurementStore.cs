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

    public async Task<MeasurementWriteResult> CreateAsync(Measurement measurement, int sensorId, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Lock the series before the sensor, matching the order used when deleting a series.
        var series = await _dbContext.Series
            .FromSqlInterpolated($"SELECT * FROM \"Series\" WHERE \"Id\" = {measurement.SeriesId} FOR SHARE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (series is null)
        {
            return MeasurementWriteResult.RevokedSensor;
        }

        var sensor = await _dbContext.Sensors
            .FromSqlInterpolated($"SELECT * FROM \"Sensors\" WHERE \"Id\" = {sensorId} AND \"SeriesId\" = {series.Id} FOR KEY SHARE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (sensor is null)
        {
            return MeasurementWriteResult.RevokedSensor;
        }

        if (!double.IsFinite(measurement.Value) || measurement.Value < series.MinValue || measurement.Value > series.MaxValue)
        {
            return MeasurementWriteResult.InvalidValue;
        }

        measurement.SensorId = sensor.Id;
        var subMicrosecondTicks = measurement.Timestamp.Ticks % TimeSpan.TicksPerMicrosecond;
        measurement.Timestamp = measurement.Timestamp.AddTicks(-subMicrosecondTicks);
        _dbContext.Measurements.Add(measurement);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MeasurementWriteResult.Created;
    }
}

public enum MeasurementWriteResult
{
    Created,
    RevokedSensor,
    InvalidValue
}
