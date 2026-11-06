using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using PitchPulse.Api.Models;

namespace PitchPulse.Api.Contracts;

public class SeriesRequest
{
    [Required, MaxLength(100)]
    public required string Name { get; init; }

    public required double MinValue { get; init; }
    public required double MaxValue { get; init; }

    [Required, StringLength(7, MinimumLength = 7), RegularExpression("^#[0-9A-Fa-f]{6}$")]
    public required string Color { get; init; }

    public string? Icon { get; init; }

    [MaxLength(20)]
    public string? Unit { get; init; }

    public int? PlayerId { get; init; }

    [EnumDataType(typeof(MetricType))]
    [JsonConverter(typeof(JsonStringEnumConverter<MetricType>))]
    public MetricType MetricType { get; init; } = MetricType.Custom;
}
