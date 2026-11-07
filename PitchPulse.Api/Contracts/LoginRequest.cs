using System.ComponentModel.DataAnnotations;

namespace PitchPulse.Api.Contracts;

public class LoginRequest
{
    [Required, MaxLength(100)]
    public required string Username { get; init; }

    [Required, MaxLength(200)]
    public required string Password { get; init; }
}
