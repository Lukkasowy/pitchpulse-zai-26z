using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PitchPulse.Api.Models;

public class Sensor
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public required string Name { get; set; }

    public int SeriesId { get; set; }

    [JsonIgnore]
    [Required, MaxLength(64)]
    public required string ApiKeyHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
