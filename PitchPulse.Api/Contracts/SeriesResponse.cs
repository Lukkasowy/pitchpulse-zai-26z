using PitchPulse.Api.Models;

namespace PitchPulse.Api.Contracts;

public class SeriesResponse
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public double MinValue { get; init; }
    public double MaxValue { get; init; }
    public required string Color { get; init; }
    public string? Icon { get; init; }
    public string? Unit { get; init; }
    public int? PlayerId { get; init; }
    public required string MetricType { get; init; }

    public static SeriesResponse FromModel(MetricSeries series)
    {
        return new SeriesResponse
        {
            Id = series.Id,
            Name = series.Name,
            MinValue = series.MinValue,
            MaxValue = series.MaxValue,
            Color = series.Color,
            Icon = series.Icon,
            Unit = series.Unit,
            PlayerId = series.PlayerId,
            MetricType = series.MetricType.ToString()
        };
    }
}
