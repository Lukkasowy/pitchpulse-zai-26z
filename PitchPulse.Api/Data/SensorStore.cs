using Microsoft.EntityFrameworkCore;
using Npgsql;
using PitchPulse.Api.Contracts;
using PitchPulse.Api.Models;
using PitchPulse.Api.Security;

namespace PitchPulse.Api.Data;

public class SensorStore(PitchPulseDbContext dbContext, TimeProvider timeProvider)
{
    public Task<List<Sensor>> GetAllAsync(CancellationToken cancellationToken)
    {
        return dbContext.Sensors.AsNoTracking().OrderBy(sensor => sensor.Id).ToListAsync(cancellationToken);
    }

    public Task<Sensor?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return dbContext.Sensors.AsNoTracking().SingleOrDefaultAsync(sensor => sensor.Id == id, cancellationToken);
    }

    public async Task<SensorCreatedResponse?> RegisterAsync(SensorRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Series.AnyAsync(series => series.Id == request.SeriesId, cancellationToken))
        {
            return null;
        }

        var apiKey = OpaqueTokens.Create();
        var sensor = new Sensor
        {
            Name = request.Name,
            SeriesId = request.SeriesId,
            ApiKeyHash = OpaqueTokens.Hash(apiKey),
            CreatedAt = timeProvider.GetUtcNow()
        };
        dbContext.Sensors.Add(sensor);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ForeignKeyViolation,
            ConstraintName: "FK_Sensors_Series_SeriesId"
        })
        {
            return null;
        }

        return new SensorCreatedResponse(sensor.Id, sensor.Name, sensor.SeriesId, sensor.CreatedAt, apiKey);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var removed = await dbContext.Sensors.Where(sensor => sensor.Id == id).ExecuteDeleteAsync(cancellationToken);
        return removed == 1;
    }
}
