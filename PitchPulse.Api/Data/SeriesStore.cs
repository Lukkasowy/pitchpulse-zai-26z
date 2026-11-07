using Microsoft.EntityFrameworkCore;
using Npgsql;
using PitchPulse.Api.Contracts;
using PitchPulse.Api.Models;

namespace PitchPulse.Api.Data;

public class SeriesStore
{
    private readonly PitchPulseDbContext _dbContext;

    public SeriesStore(PitchPulseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<MetricSeries>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Series.AsNoTracking().OrderBy(series => series.Id).ToListAsync(cancellationToken);
    }

    public async Task<MetricSeries?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _dbContext.Series.AsNoTracking().FirstOrDefaultAsync(series => series.Id == id, cancellationToken);
    }

    public async Task<SeriesWriteResult> CreateAsync(SeriesRequest request, CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(request, cancellationToken);
        if (error != SeriesWriteError.None)
        {
            return new SeriesWriteResult(null, error);
        }

        var series = new MetricSeries { Name = request.Name, Color = request.Color };
        ApplyValues(series, request);
        _dbContext.Series.Add(series);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsMissingPlayer(exception))
        {
            return new SeriesWriteResult(null, SeriesWriteError.InvalidPlayer);
        }

        return new SeriesWriteResult(series);
    }

    public async Task<SeriesWriteResult> UpdateAsync(int id, SeriesRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var series = await _dbContext.Series
            .FromSqlInterpolated($"SELECT * FROM \"Series\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (series is null)
        {
            return new SeriesWriteResult(null, SeriesWriteError.NotFound);
        }

        var error = await ValidateAsync(request, cancellationToken);
        if (error != SeriesWriteError.None)
        {
            return new SeriesWriteResult(null, error);
        }

        var excludesMeasurements = await _dbContext.Measurements.AnyAsync(
            measurement => measurement.SeriesId == id && (measurement.Value < request.MinValue || measurement.Value > request.MaxValue),
            cancellationToken);

        if (excludesMeasurements)
        {
            return new SeriesWriteResult(null, SeriesWriteError.RangeConflict);
        }

        ApplyValues(series, request);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsMissingPlayer(exception))
        {
            return new SeriesWriteResult(null, SeriesWriteError.InvalidPlayer);
        }

        await transaction.CommitAsync(cancellationToken);
        return new SeriesWriteResult(series);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var removed = await _dbContext.Series.Where(series => series.Id == id).ExecuteDeleteAsync(cancellationToken);
        return removed == 1;
    }

    private async Task<SeriesWriteError> ValidateAsync(SeriesRequest request, CancellationToken cancellationToken)
    {
        if (!double.IsFinite(request.MinValue) || !double.IsFinite(request.MaxValue) || request.MinValue >= request.MaxValue)
        {
            return SeriesWriteError.InvalidRange;
        }

        if (request.PlayerId.HasValue && !await _dbContext.Players.AnyAsync(player => player.Id == request.PlayerId.Value, cancellationToken))
        {
            return SeriesWriteError.InvalidPlayer;
        }

        return SeriesWriteError.None;
    }

    private static void ApplyValues(MetricSeries series, SeriesRequest request)
    {
        series.Name = request.Name;
        series.MinValue = request.MinValue;
        series.MaxValue = request.MaxValue;
        series.Color = request.Color;
        series.Icon = request.Icon;
        series.Unit = request.Unit;
        series.PlayerId = request.PlayerId;
        series.MetricType = request.MetricType;
    }

    private static bool IsMissingPlayer(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ForeignKeyViolation,
            ConstraintName: "FK_Series_Players_PlayerId"
        };
    }
}
