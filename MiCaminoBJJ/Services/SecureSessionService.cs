using Microsoft.Extensions.DependencyInjection;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;

namespace MiCaminoBJJ.Services;

/// <summary>
/// Session service backed by MAUI SecureStorage for persistent session.
/// Infrastructure's SessionService is in-memory only; this replaces it
/// in the App layer to provide actual persistence via SecureStorage.
/// </summary>
public class SecureSessionService : ISessionService
{
    private const string UserIdKey = "CurrentUserId";
    private int? _cachedUserId;
    private UserRole? _cachedUserRole;
    private bool _roleLoaded;

    public async Task<int?> GetCurrentUserIdAsync()
    {
        if (_cachedUserId.HasValue)
            return _cachedUserId;

        try
        {
            var value = await SecureStorage.GetAsync(UserIdKey);
            if (int.TryParse(value, out var userId))
            {
                _cachedUserId = userId;
                return userId;
            }
        }
        catch
        {
            // SecureStorage may fail on some platforms
        }

        return null;
    }

    public async Task SetCurrentUserIdAsync(int userId)
    {
        _cachedUserId = userId;
        try
        {
            await SecureStorage.SetAsync(UserIdKey, userId.ToString());
        }
        catch
        {
            // Fail silently - in-memory cache still works for current session
        }
    }

    public async Task ClearSessionAsync()
    {
        _cachedUserId = null;
        _cachedUserRole = null;
        _roleLoaded = false;
        try
        {
            SecureStorage.Remove(UserIdKey);
        }
        catch
        {
            // Fail silently
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        if (_cachedUserId.HasValue)
            return true;

        var userId = await GetCurrentUserIdAsync();
        return userId.HasValue;
    }

    public async Task<UserRole?> GetUserRoleAsync()
    {
        if (_roleLoaded)
            return _cachedUserRole;

        var userId = await GetCurrentUserIdAsync();
        if (!userId.HasValue)
            return null;

        try
        {
            using var scope = IPlatformApplication.Current?.Services?.CreateScope();
            var userService = scope?.ServiceProvider?.GetService<IUserService>();
            if (userService != null)
            {
                var user = await userService.GetByIdAsync(userId.Value);
                _cachedUserRole = user?.Role;
                _roleLoaded = true;
                return _cachedUserRole;
            }
        }
        catch
        {
            // Fail silently - role resolution failure should not break the app
        }

        return null;
    }
}
