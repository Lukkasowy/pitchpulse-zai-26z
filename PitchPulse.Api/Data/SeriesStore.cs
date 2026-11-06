using Microsoft.EntityFrameworkCore;
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
}
