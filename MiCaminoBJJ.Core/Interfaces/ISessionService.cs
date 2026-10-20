using MiCaminoBJJ.Core.Enums;

namespace MiCaminoBJJ.Core.Interfaces;

public interface ISessionService
{
    Task<int?> GetCurrentUserIdAsync();
    Task SetCurrentUserIdAsync(int userId);
    Task ClearSessionAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<UserRole?> GetUserRoleAsync();
}
