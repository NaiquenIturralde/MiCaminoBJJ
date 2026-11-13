using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using MiCaminoBJJ.Web.Services.Admin;

namespace MiCaminoBJJ.Web.Security;

/// <summary>
/// Acceso administrativo LOCAL de Development (Etapa 3, sin cambios de
/// comportamiento; la configuración se movió desde Program.cs en la Etapa 4B
/// para poder probarla y para registrar aquí la política AdminLocal).
///
/// Esquema de autenticación: CookieAuthenticationDefaults.AuthenticationScheme
/// ("Cookies") — cookie de navegador "MiCaminoBJJ.Admin". Es el ÚNICO esquema
/// con el que se autoriza el CRUD (vía la política <see cref="AdminPolicies.AdminLocal"/>).
/// </summary>
public static class AdminAuthSetup
{
    public static IServiceCollection AddAdminAuth(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/AdminAcceso/Login";
                options.LogoutPath = "/AdminAcceso/Logout";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = false;
                options.Cookie.Name = "MiCaminoBJJ.Admin";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.IsEssential = true;
                options.ReturnUrlParameter = "returnUrl";
                // Autenticado SIN rol Administrador → misma página local que
                // Identity: el Forbid multi-esquema termina en una ruta real.
                options.AccessDeniedPath = IdentitySetup.AccessDeniedPath;
            });

        // Autorización (Etapa 4C.1/5): políticas EXPLÍCITAS, cada una anclada
        // a su(es) esquema — el aislamiento NO depende del esquema por defecto
        // ni de ningún middleware manual:
        // - AdminLocal: SOLO esquema "Cookies" (admin de Development) + rol
        //   Administrador. Sigue activa para el logout del módulo (acciones
        //   exclusivas de la cookie administrativa).
        // - AdministradorContenido: "Identity.Application" + "Cookies" con el
        //   MISMO rol Administrador → el panel acepta una cuenta Identity
        //   administrativa SIN retirar la cookie temporal de Development.
        // - PracticanteAutenticado: esquema "Identity.Application" + rol
        //   Practicante (asignado solo en el servidor al registrarse).
        // - UsuarioAplicante: cualquier sesión de Identity (guía del practicante).
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AdminPolicies.AdminLocal, AdminPolicies.Build());
            options.AddPolicy(AdminPolicies.AdministradorContenido, AdminPolicies.BuildAdministradorContenido());
            options.AddPolicy(AdminPolicies.ConsultarAuditoria, AdminPolicies.BuildConsultarAuditoria());
            // Lectura de auditoría (pantalla + PDF): rol "Administrador" con los
            // dos esquemas (Identity y cookie de Development).
            options.AddPolicy(AdminPolicies.AdministradorAuditoria, AdminPolicies.BuildAdministradorAuditoria());
            options.AddPolicy(IdentitySetup.PoliticaPracticante, new AuthorizationPolicyBuilder(IdentitySetup.AuthSchemeName)
                .RequireAuthenticatedUser()
                .RequireRole(IdentitySetup.RolPracticante)
                .Build());
            // Etapa 5: Guía del Practicante — cualquier sesión de Identity
            // autenticada (esquema Identity.Application, sin rol específico).
            options.AddPolicy(IdentitySetup.PoliticaUsuarioAutenticado, new AuthorizationPolicyBuilder(IdentitySetup.AuthSchemeName)
                .RequireAuthenticatedUser()
                .Build());
        });

        return services;
    }
}
