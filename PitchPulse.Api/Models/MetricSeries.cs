using System.ComponentModel.DataAnnotations;

namespace PitchPulse.Api.Models;

public class MetricSeries
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public required string Name { get; set; }

    public int? PlayerId { get; set; }
    public Player? Player { get; set; }

    public MetricType MetricType { get; set; } = MetricType.Custom;

    public double MinValue { get; set; }
    public double MaxValue { get; set; }

    [MaxLength(20)]
    public string? Unit { get; set; }

    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$")]
    public required string Color { get; set; }

    public string? Icon { get; set; }
}
