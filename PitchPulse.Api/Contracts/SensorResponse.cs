using PitchPulse.Api.Models;

namespace PitchPulse.Api.Contracts;

public record SensorResponse(int Id, string Name, int SeriesId, DateTimeOffset CreatedAt)
{
    public static SensorResponse FromModel(Sensor sensor)
    {
        return new SensorResponse(sensor.Id, sensor.Name, sensor.SeriesId, sensor.CreatedAt);
    }
}

public record SensorCreatedResponse(int Id, string Name, int SeriesId, DateTimeOffset CreatedAt, string ApiKey)
    : SensorResponse(Id, Name, SeriesId, CreatedAt);
