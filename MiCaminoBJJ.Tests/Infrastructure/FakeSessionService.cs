using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;

namespace MiCaminoBJJ.Tests.Infrastructure;

/// <summary>
/// ISessionService configurable para pruebas (Etapa 1.2).
/// Permite simular: sin sesión, practicante, administrador, etc.
/// </summary>
public sealed class FakeSessionService : ISessionService
{
    public int? UserId { get; set; }
    public UserRole? Role { get; set; }

    public Task<int?> GetCurrentUserIdAsync() => Task.FromResult(UserId);

    public Task SetCurrentUserIdAsync(int userId)
    {
        UserId = userId;
        return Task.CompletedTask;
    }

    public Task ClearSessionAsync()
    {
        UserId = null;
        Role = null;
        return Task.CompletedTask;
    }

    public Task<bool> IsAuthenticatedAsync() => Task.FromResult(UserId.HasValue);

    public Task<UserRole?> GetUserRoleAsync() => Task.FromResult(Role);
}
