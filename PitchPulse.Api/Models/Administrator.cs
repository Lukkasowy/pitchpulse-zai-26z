using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PitchPulse.Api.Models;

public class Administrator
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public required string Username { get; set; }

    [JsonIgnore]
    [Required, MaxLength(200)]
    public required string PasswordHash { get; set; }
}
