using System.Security.Cryptography;
using System.Text;

namespace PitchPulse.Api.Security;

public static class OpaqueTokens
{
    public static string Create()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    public static string Hash(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
