using System.ComponentModel.DataAnnotations;

namespace PitchPulse.Api.Models;

public class Player
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public required string FirstName { get; set; }

    [Required, MaxLength(100)]
    public required string LastName { get; set; }

    public int JerseyNumber { get; set; }
}
