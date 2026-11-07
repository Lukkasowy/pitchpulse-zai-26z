using System.ComponentModel.DataAnnotations;

namespace PitchPulse.Api.Contracts;

public class SensorRequest
{
    [Required, MaxLength(100)]
    public required string Name { get; init; }

    public required int SeriesId { get; init; }
}
