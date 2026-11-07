using Npgsql;

namespace PitchPulse.Api.Data;

internal static class DatabaseConnection
{
    public static string FromConfiguration(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PitchPulse");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        var databaseUrl = configuration["DATABASE_URL"];
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            throw new InvalidOperationException("Brak konfiguracji ConnectionStrings:PitchPulse lub DATABASE_URL.");
        }

        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var url) ||
            url.Scheme is not ("postgresql" or "postgres") || url.Host.Length == 0 ||
            url.AbsolutePath.Length < 2 || url.Query.Length != 0 || url.Fragment.Length != 0)
        {
            throw new InvalidOperationException("DATABASE_URL musi być wewnętrznym adresem PostgreSQL z Render.");
        }

        var credentials = url.UserInfo.Split(':', 2);
        if (credentials.Length != 2 || credentials[0].Length == 0 || credentials[1].Length == 0)
        {
            throw new InvalidOperationException("DATABASE_URL musi zawierać nazwę użytkownika i hasło.");
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = url.IdnHost,
            Port = url.IsDefaultPort ? 5432 : url.Port,
            Database = Uri.UnescapeDataString(url.AbsolutePath[1..]),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1]),
            GssEncryptionMode = GssEncryptionMode.Disable,
            MaxPoolSize = 20
        }.ConnectionString;
    }
}
