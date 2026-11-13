using MiCaminoBJJ.Web.Filters;
using MiCaminoBJJ.Web.Security;
using MiCaminoBJJ.Web.Services.Admin;
using MiCaminoBJJ.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MiCaminoBJJ.Web.Controllers;

/// <summary>
/// Acceso administrativo LOCAL de Development (Etapa 3).
///
/// Mecanismo: contraseña de desarrollo guardada FUERA del repositorio
/// (User Secrets, clave AdminDev:Password) validada en el SERVIDOR con
/// comparación de tiempo fijo; si es correcta se emite una cookie de
/// autenticación de ASP.NET Core (firmada, HttpOnly, no persistente).
/// Fuera de Development todo esto responde HTTP 404 (filtro AdminSoloDevelopment).
///
/// NO es Identity ni administración de usuarios: no hay usuarios ficticios,
/// no hay roles en base de datos y la credencial jamás se almacena en el repo.
/// </summary>
[AdminSoloDevelopment]
public class AdminAccesoController : Controller
{
    private const string InhabilitadoMensaje =
        "El módulo administrativo está deshabilitado: requiere entorno Development "
        + "y la credencial AdminDev:Password configurada fuera del repositorio "
        + "(dotnet user-secrets set \"AdminDev:Password\" \"<valor>\").";

    private readonly IDevAdminCredentialValidator _credencial;
    private readonly IDevAdminSession _sesion;
    private readonly ILogger<AdminAccesoController> _logger;

    public AdminAccesoController(
        IDevAdminCredentialValidator credencial,
        IDevAdminSession sesion,
        ILogger<AdminAccesoController> logger)
    {
        _credencial = credencial ?? throw new ArgumentNullException(nameof(credencial));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (!_credencial.EstaHabilitado)
            ModelState.AddModelError(string.Empty, InhabilitadoMensaje);

        return View(new AdminLoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(AdminLoginViewModel model)
    {
        if (!_credencial.EstaHabilitado)
        {
            // Módulo sin credencial: no se valida ni se emite cookie.
            ModelState.AddModelError(string.Empty, InhabilitadoMensaje);
            return View(model);
        }

        if (!ModelState.IsValid)
            return View(model);

        if (!_credencial.PasswordValido(model.Password))
        {
            // Validación SIEMPRE en el servidor; intento queda en el log (sin la contraseña).
            _logger.LogWarning(
                "Intento de acceso administrativo fallido desde {Remoto}.",
                HttpContext.Connection.RemoteIpAddress);
            ModelState.AddModelError(string.Empty, "Credencial incorrecta.");
            return View(model);
        }

        await _sesion.SignInAsync(HttpContext, _credencial.Usuario);
        _logger.LogInformation("Acceso administrativo local concedido a {Usuario}.", _credencial.Usuario);

        // Solo URLs locales: nunca redirigir a un sitio externo (open redirect).
        return LocalRedirect(string.IsNullOrWhiteSpace(model.ReturnUrl) || !Url.IsLocalUrl(model.ReturnUrl)
            ? "/AdminTecnicas"
            : model.ReturnUrl);
    }

    /// <summary>
    /// Cierre de sesión administrativa (Etapa 4C.1, Etapa 5).
    /// Usa la MISMA política que el panel (AdministradorContenido: Identity +
    /// Cookies con rol Administrador) para que el antiforgery del token emitido
    /// en /AdminTecnicas valide contra el mismo principal. El logout de
    /// prácticos (Cuenta/Logout) es independiente y no se toca aquí.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.AdministradorContenido)]
    public async Task<IActionResult> Logout()
    {
        await _sesion.SignOutAsync(HttpContext);
        return RedirectToAction(nameof(Login));
    }
}
