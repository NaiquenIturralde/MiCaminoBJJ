using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;

namespace MiCaminoBJJ.Infrastructure.Services;

public class SessionService : ISessionService
{
    private int? _currentUserId;

    public Task<int?> GetCurrentUserIdAsync()
    {
        return Task.FromResult(_currentUserId);
    }

    public Task SetCurrentUserIdAsync(int userId)
    {
        _currentUserId = userId;
        return Task.CompletedTask;
    }

    public Task ClearSessionAsync()
    {
        _currentUserId = null;
        return Task.CompletedTask;
    }

    public Task<bool> IsAuthenticatedAsync()
    {
        return Task.FromResult(_currentUserId.HasValue);
    }

    public Task<UserRole?> GetUserRoleAsync()
    {
        // In-memory implementation does not resolve the role.
        return Task.FromResult<UserRole?>(null);
    }
}
