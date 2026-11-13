using MiCaminoBJJ.Web.Identity;
using MiCaminoBJJ.Web.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace MiCaminoBJJ.Web.Security;

/// <summary>
/// Configuración de ASP.NET Core Identity para la web (Etapa 4B).
///
/// Alcance de esta etapa: SOLO infraestructura (esquema, opciones, stores y
/// asociación con el dominio). No registra pantallas ni flujos de
/// registro/login de practicantes (Etapa 4C) ni asigna roles (Etapa 4D).
///
/// Seguridad:
/// - Hash: proveedor estándar de Identity (PBKDF2), sin texto plano ni
///   cifrado reversible; contraseñas de MAUI jamás se comparten aquí.
/// - Requisitos de contraseña y bloqueo configurados explícitamente abajo.
/// - Tokens (restablecimiento/confirmación): proveedor estándar de Identity
///   (DataProtector), seguro por defecto.
/// - Confirmación de correo PREPARADA pero sin envío: no hay SMTP
///   registrado y <c>RequireConfirmedEmail = false</c> para no bloquear el
///   flujo futuro; se activará junto con el envío real (Etapa 4C/decisión).
/// - Sin usuarios ni roles predeterminados: cero HasData/seed en Identity.
/// - Cookie de sesión pública CON NOMBRE PROPIO ("MiCaminoBJJ.Cuenta"),
///   distinta de la cookie admin ("MiCaminoBJJ.Admin") — sin duplicación.
/// </summary>
public static class IdentitySetup
{
    /// <summary>Nombre de la cookie de sesión pública (futura Etapa 4C).</summary>
    public const string AppCookieName = "MiCaminoBJJ.Cuenta";

    /// <summary>Ruta futura de login público (aún no existe vista).</summary>
    public const string LoginPath = "/Cuenta/Login";

    /// <summary>Página local de "acceso denegado" (autenticado sin rol requerido).</summary>
    public const string AccessDeniedPath = "/Cuenta/AccesoDenegado";

    /// <summary>Nombre del esquema de autenticación de Identity (cookie de práctico).</summary>
    public static readonly string AuthSchemeName = IdentityConstants.ApplicationScheme; // "Identity.Application"

    /// <summary>Rol de prácticos: se asigna SOLO en el servidor al registrarse.</summary>
    public const string RolPracticante = "Practicante";

    /// <summary>Nombre de la política de autorización de prácticos autenticados (Etapa 4C.1).</summary>
    public const string PoliticaPracticante = "PracticanteAutenticado";

    /// <summary>
    /// Política para cualquier sesión de Identity autenticada (Etapa 5):
    /// usada por el Guía del Practicante. Sin exigir rol, para que sirva tanto
    /// a prácticos como a cuentas administrativas futuras.
    /// </summary>
    public const string PoliticaUsuarioAutenticado = "UsuarioAutenticado";

    public static IServiceCollection AddWebIdentity(this IServiceCollection services)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            // ── Requisitos de contraseña (configurables, explícitos) ──
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequiredUniqueChars = 1;

            // ── Bloqueo temporal tras intentos fallidos ──
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.AllowedForNewUsers = true;

            // ── Cuentas ──
            options.User.RequireUniqueEmail = true;

            // ── Confirmación de correo preparada, sin envío habilitado ──
            // (no hay SMTP registrado; el envío real llega en una etapa posterior
            // y entonces podrá requerirse RequireConfirmedEmail.)
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddEntityFrameworkStores<WebTfDbContext>()
        // ── Etapa 4D-C: proveedores de tokens de Identity ──
        // SIN esto, GeneratePasswordResetTokenAsync (y cualquier token de
        // confirmación/2FA) lanza:
        //   "No IUserTwoFactorTokenProvider<TUser> named 'Default' is registered."
        // AddIdentity NO los registra por sí solo. Es un registro ADITIVO
        // (EmailTokenProvider, PhoneNumberTokenProvider, AuthenticatorTokenProvider
        // y el proveedor 'Default'), firmado con DataProtection; no altera el
        // login, el hash ni las cookies existentes. Lo exige el comando
        // reset-admin-password, que usa el flujo OFICIAL de restablecimiento.
        .AddDefaultTokenProviders();

        // Cookie de la sesión pública: HttpOnly, SameSite=Lax, Secure según
        // transporte (se vuelve Secure bajo HTTPS), expiración acotada con
        // renovación por actividad. Se configura DIRECTAMENTE sobre el esquema
        // "Identity.Application" para que prevalezca sobre los defaults.
        services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.Name = AppCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.IsEssential = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;      // renovación controlada por actividad
            options.LoginPath = LoginPath;
            // Autenticado SIN el rol requerido → página local real (nunca una
            // ruta inexistente como /Account/AccessDenied).
            options.AccessDeniedPath = AccessDeniedPath;
        });

        return services;
    }
}
