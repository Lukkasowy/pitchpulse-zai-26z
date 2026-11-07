using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PitchPulse.Api.Models;

public class AdminSession
{
    public int Id { get; set; }
    public int AdministratorId { get; set; }

    [JsonIgnore]
    [Required, MaxLength(64)]
    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
