using System.Text.Json.Serialization;

namespace PitchPulse.Api.Models;

public class Measurement
{
    public int Id { get; set; }
    public int SeriesId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SensorId { get; set; }
    public double Value { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
