using System.ComponentModel.DataAnnotations;

namespace PitchPulse.Api.Contracts;

public class PasswordChangeRequest
{
    [Required, MaxLength(200)]
    public required string CurrentPassword { get; init; }

    [Required, MaxLength(200)]
    public required string NewPassword { get; init; }
}
