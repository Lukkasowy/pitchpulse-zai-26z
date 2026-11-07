using System.Text;
using Microsoft.EntityFrameworkCore;
using PitchPulse.Api.Contracts;
using PitchPulse.Api.Data;
using PitchPulse.Api.Models;
using PitchPulse.Api.Security;

namespace PitchPulse.Api.Services;

public class AuthService
{
    public const int SessionLifetimeSeconds = 3600;
    private static readonly string DummyPasswordHash = BCrypt.Net.BCrypt.HashPassword("UnusedAccountPassword", workFactor: 12);
    private readonly PitchPulseDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public AuthService(PitchPulseDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<LoginResponse?> LoginAsync(string username, string password, CancellationToken cancellationToken)
    {
        if (Encoding.UTF8.GetByteCount(password) > 72)
        {
            return null;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var administrator = await _dbContext.Administrators
            .FromSqlInterpolated($"SELECT * FROM \"Administrators\" WHERE \"Username\" = {username} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        var passwordMatches = BCrypt.Net.BCrypt.Verify(password, administrator?.PasswordHash ?? DummyPasswordHash);
        if (administrator is null || !passwordMatches)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        await _dbContext.AdminSessions
            .Where(session => session.AdministratorId == administrator.Id && session.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);

        var token = OpaqueTokens.Create();
        _dbContext.AdminSessions.Add(new AdminSession
        {
            AdministratorId = administrator.Id,
            TokenHash = OpaqueTokens.Hash(token),
            CreatedAt = now,
            ExpiresAt = now.AddSeconds(SessionLifetimeSeconds)
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new LoginResponse { AccessToken = token, ExpiresIn = SessionLifetimeSeconds };
    }

    public async Task<bool> LogoutAsync(int administratorId, int sessionId, CancellationToken cancellationToken)
    {
        var removed = await _dbContext.AdminSessions
            .Where(session => session.Id == sessionId && session.AdministratorId == administratorId)
            .ExecuteDeleteAsync(cancellationToken);
        return removed == 1;
    }

    public async Task<PasswordChangeResult> ChangePasswordAsync(int administratorId, int sessionId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var administrator = await _dbContext.Administrators
            .FromSqlInterpolated($"SELECT * FROM \"Administrators\" WHERE \"Id\" = {administratorId} FOR UPDATE")
            .SingleAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow();
        var sessionIsValid = await _dbContext.AdminSessions.AnyAsync(
            session => session.Id == sessionId && session.AdministratorId == administratorId && session.ExpiresAt > now,
            cancellationToken);

        if (!sessionIsValid)
        {
            return PasswordChangeResult.InvalidSession;
        }

        if (Encoding.UTF8.GetByteCount(currentPassword) > 72 || !BCrypt.Net.BCrypt.Verify(currentPassword, administrator.PasswordHash))
        {
            return PasswordChangeResult.InvalidCurrentPassword;
        }

        administrator.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);
        await _dbContext.AdminSessions.Where(session => session.AdministratorId == administratorId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PasswordChangeResult.Success;
    }
}

public enum PasswordChangeResult
{
    Success,
    InvalidSession,
    InvalidCurrentPassword
}
