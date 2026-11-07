using System.Text.Json;
using System.Text.Json.Serialization;

namespace PitchPulse.Api.Contracts;

public class MeasurementRequest
{
    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public required double Value { get; init; }

    public JsonElement Timestamp { get; init; }
}
