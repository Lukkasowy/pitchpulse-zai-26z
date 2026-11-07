using System.Text;
using Microsoft.EntityFrameworkCore;
using PitchPulse.Api.Data;
using PitchPulse.Api.Models;

namespace PitchPulse.Api.Services;

public static class AdministratorSetup
{
    public static async Task CreateAsync(PitchPulseDbContext dbContext, IConfiguration configuration)
    {
        var username = configuration["Admin:Username"];
        if (string.IsNullOrWhiteSpace(username) || username.Length > 100)
        {
            throw new InvalidOperationException("Ustaw Admin:Username (maksimum 100 znaków).");
        }

        if (await dbContext.Administrators.AnyAsync(administrator => administrator.Username == username))
        {
            Console.WriteLine("Administrator już istnieje. Jego hasło nie zostało zmienione.");
            return;
        }

        var password = configuration["Admin:Password"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || Encoding.UTF8.GetByteCount(password) > 72)
        {
            throw new InvalidOperationException("Ustaw Admin:Password (minimum 8 znaków, maksimum 72 bajty UTF-8).");
        }

        dbContext.Administrators.Add(new Administrator
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12)
        });
        await dbContext.SaveChangesAsync();
        Console.WriteLine("Utworzono konto administratora.");
    }
}
