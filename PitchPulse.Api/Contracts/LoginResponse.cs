namespace PitchPulse.Api.Contracts;

public class LoginResponse
{
    public required string AccessToken { get; init; }
    public string TokenType { get; init; } = "Bearer";
    public int ExpiresIn { get; init; }
}
