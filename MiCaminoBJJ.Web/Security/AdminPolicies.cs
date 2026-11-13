using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using MiCaminoBJJ.Web.Services.Admin;

namespace MiCaminoBJJ.Web.Security;

/// <summary>
/// Políticas de autorización del acceso ADMINISTRATIVO (Etapa 4B/4D-C).
/// </summary>
public static class AdminPolicies
{
    /// <summary>Nombre de la política de las acciones administrativas.</summary>
    public const string AdminLocal = "AdminLocal";

    /// <summary>Política combinada para el panel administrativo (convivencia).</summary>
    public const string AdministradorContenido = "AdministradorContenido";

    /// <summary>Política exclusiva para consulta de auditoría con Identity real.</summary>
    public const string ConsultarAuditoria = "ConsultarAuditoria";

    /// <summary>
    /// Política de LECTURA de la auditoría (pantalla de Auditoría + descarga
    /// del PDF): exige sesión autenticada con el rol "Administrador".
    /// Acepta el esquema "Identity.Application" (cuenta Identity real) y la
    /// cookie administrativa de Development ("Cookies"), igual que el panel
    /// administrativo. Un Practicante queda FUERA (403) en ambos esquemas.
    /// </summary>
    public const string AdministradorAuditoria = "AdministradorAuditoria";

    /// <summary>Esquema de autenticación EXACTO del acceso administrativo actual.</summary>
    public const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme; // "Cookies"

    public static AuthorizationPolicy Build() =>
        new AuthorizationPolicyBuilder(Scheme)
            .RequireAuthenticatedUser()
            .RequireRole(DevAdminCredential.RoleClaim) // "Administrador"
            .Build();

    /// <summary>
    /// Política del panel administrativo: acepta la cookie temporal de
    /// Development (esquema "Cookies") Y la sesión de Identity
    /// ("Identity.Application"), siempre con el rol Administrador.
    ///
    /// ORDEN DE ESQUEMAS IMPORTANTE: el resultado de autorización desafía (o
    /// prohíbe) esquema por esquema en este orden; el ÚLTIMO es el que define el
    /// Location final del challenge. "Cookies" va al final para conservar el
    /// redireccionamiento histórico a /AdminAcceso/Login (flujo de Development)
    /// cuando el visitante es anónimo.
    /// </summary>
    public static AuthorizationPolicy BuildAdministradorContenido() =>
        new AuthorizationPolicyBuilder(
                IdentityConstants.ApplicationScheme,
                CookieAuthenticationDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireRole(DevAdminCredential.RoleClaim) // "Administrador"
            .Build();

    public static AuthorizationPolicy BuildConsultarAuditoria() =>
        new AuthorizationPolicyBuilder(IdentityConstants.ApplicationScheme)
            .RequireAuthenticatedUser()
            .RequireRole(DevAdminCredential.RoleClaim) // "Administrador"
            .Build();

    /// <summary>
    /// Política de lectura de auditoría: MISMO requisito de rol que el panel
    /// (rol "Administrador"), con los dos esquemas (Identity + cookie de
    /// Development) para que la pantalla sea accesible con cualquiera de las
    /// dos formas de ingreso administrativo. Nunca admite a un Practicante.
    /// </summary>
    public static AuthorizationPolicy BuildAdministradorAuditoria() =>
        new AuthorizationPolicyBuilder(
                IdentityConstants.ApplicationScheme,
                CookieAuthenticationDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireRole(DevAdminCredential.RoleClaim) // "Administrador"
            .Build();
}
