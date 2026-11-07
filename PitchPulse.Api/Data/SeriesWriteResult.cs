using PitchPulse.Api.Models;

namespace PitchPulse.Api.Data;

public record SeriesWriteResult(MetricSeries? Series, SeriesWriteError Error = SeriesWriteError.None);

public enum SeriesWriteError
{
    None,
    NotFound,
    InvalidRange,
    InvalidPlayer,
    RangeConflict
}
