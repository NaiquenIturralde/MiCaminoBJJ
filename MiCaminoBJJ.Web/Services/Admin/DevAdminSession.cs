using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MiCaminoBJJ.Web.Services.Admin;

/// <summary>
/// Emisión/cierre de la sesión administrativa de Development (Etapa 3).
///
/// Separado del controlador para poder probar el comportamiento de login sin
/// montar el pipeline de autenticación completo. La cookie es emitida por
/// ASP.NET Core (firmada por el Data Protection del framework), HTTPONLY,
/// SameSite=Lax, no persistente y con expiración acotada.
/// </summary>
public interface IDevAdminSession
{
    Task SignInAsync(HttpContext httpContext, string usuario);
    Task SignOutAsync(HttpContext httpContext);
}

public sealed class DevAdminSession : IDevAdminSession
{
    private static readonly TimeSpan Duracion = TimeSpan.FromHours(8);

    public async Task SignInAsync(HttpContext httpContext, string usuario)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(usuario);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, usuario),
            new Claim(ClaimTypes.Role, DevAdminCredential.RoleClaim)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = false,
                ExpiresUtc = DateTimeOffset.UtcNow.Add(Duracion)
            });
    }

    public Task SignOutAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
