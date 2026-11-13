using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Web.Identity;
using MiCaminoBJJ.Web.Localization;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Security;
using MiCaminoBJJ.Web.Services.Perfil;
using MiCaminoBJJ.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace MiCaminoBJJ.Web.Controllers;

/// <summary>
/// Cuenta de prácticos (Etapa 4C): registro, inicio y cierre de sesión.
///
/// Seguridad:
/// - Credenciales validadas EXCLUSIVAMENTE en el servidor con Identity
///   (PBKDF2, bloqueo por intentos); nunca se registran contraseñas ni
///   tokens en logs.
/// - Mensajes genéricos para credenciales inválidas (sin enumeración de
///   cuentas) y bloqueo explícito informado.
/// - Sin campos de rol en los formularios: el rol Practicante se asigna
///   SOLO en el servidor; jamás se crean administradores desde aquí.
/// - Antiforgery en todos los POST; redirecciones solo a URLs locales.
/// - Sesión de Identity (cookie MiCaminoBJJ.Cuenta, esquema
///   Identity.Application) SEPARADA de la cookie admin (esquema "Cookies"):
///   una sesión de práctico no autoriza el CRUD (política AdminLocal) y el
///   logout de aquí NO toca la sesión administrativa de Development.
///
/// Transaccionalidad: la creación Identity + perfil de dominio (Users) +
/// vinculación DomainUserId + rol Practicante ocurren en UNA transacción
/// explícita sobre el WebTfDbContext compartido; los SaveChanges internos de
/// UserManager/RoleManager participan en ella. Cualquier fallo revierte TODO
/// (no quedan usuarios Identity sin perfil BJJ ni perfiles huérfanos).
///
/// Cinturón: dato de PERFIL escogido por el practicante en el formulario
/// (obligatorio y validado contra la lista del dominio en el ViewModel). Es
/// solo personalización/filtros: el rol SIEMPRE se asigna en el servidor como
/// Practicante y el cliente jamás puede elegirlo ni modificarlo.
/// </summary>
public class CuentaController : Controller
{
    private const string CredencialesInvalidasMensaje = "Cuenta.Mensaje.CredencialesInvalidas";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly WebTfDbContext _db;
    private readonly IPerfilFotoService _fotos;
    private readonly IPreferenciaIdiomaService _preferenciasIdioma;
    private readonly IStringLocalizer<UiResources> _localizador;
    private readonly ILogger<CuentaController> _logger;

    public CuentaController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        WebTfDbContext db,
        IPerfilFotoService fotos,
        IPreferenciaIdiomaService preferenciasIdioma,
        IStringLocalizer<UiResources> localizador,
        ILogger<CuentaController> logger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _fotos = fotos ?? throw new ArgumentNullException(nameof(fotos));
        _preferenciasIdioma = preferenciasIdioma ?? throw new ArgumentNullException(nameof(preferenciasIdioma));
        _localizador = localizador ?? throw new ArgumentNullException(nameof(localizador));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ── Registro ──────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Registro() => View(new RegistroViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registro(RegistroViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var email = model.Email.Trim();

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            ModelState.AddModelError(nameof(RegistroViewModel.Email), _localizador["Cuenta.Mensaje.EmailExistente"]);
            return View(model);
        }

        var identity = new ApplicationUser { UserName = email, Email = email };

        // Transacción explícita sobre el contexto compartido: los SaveChanges
        // de UserManager (CreateAsync/UpdateAsync/AddToRoleAsync) y RoleManager
        // participan en ella; cualquier return anterior al Commit revierte todo.
        await using var transaccion = await _db.Database.BeginTransactionAsync();

        var creacion = await _userManager.CreateAsync(identity, model.Password);
        if (!creacion.Succeeded)
        {
            AgregarErroresIdentity(creacion.Errors, esRegistro: true);
            return View(model); // sin Commit → rollback (aunque no haya persistido nada aún)
        }

        try
        {
            // Perfil de dominio con los campos mínimos reales de Users.
            // PasswordHash/PasswordSalt quedan vacíos POR DISEÑO: las
            // credenciales viven en AspNetUsers y no se duplican jamás.
            var perfil = new User
            {
                Name = model.Nombre.Trim(),
                Email = email,
                PasswordHash = string.Empty,
                PasswordSalt = string.Empty,
                // Cinturón elegido en el formulario (obligatorio y validado
                // contra la lista del dominio): dato de perfil, no de rol.
                BeltRank = model.Cinturon.Trim(),
                Role = UserRole.Practicante,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(perfil);
            await _db.SaveChangesAsync();

            identity.DomainUserId = perfil.Id;
            var vinculo = await _userManager.UpdateAsync(identity);
            if (!vinculo.Succeeded)
                throw new InvalidOperationException(
                    "No se pudo vincular el perfil de dominio: " + string.Join("; ", vinculo.Errors.Select(e => e.Description)));

            // Rol Practicante: seeding controlado e idempotente (NUNCA se crea
            // un rol Administrador desde este flujo).
            if (!await _roleManager.RoleExistsAsync(IdentitySetup.RolPracticante))
            {
                var creacionRol = await _roleManager.CreateAsync(new IdentityRole(IdentitySetup.RolPracticante));
                if (!creacionRol.Succeeded)
                    throw new InvalidOperationException(
                        "No se pudo crear el rol Practicante: " + string.Join("; ", creacionRol.Errors.Select(e => e.Description)));
            }

            var asignacion = await _userManager.AddToRoleAsync(identity, IdentitySetup.RolPracticante);
            if (!asignacion.Succeeded)
                throw new InvalidOperationException(
                    "No se pudo asignar el rol Practicante: " + string.Join("; ", asignacion.Errors.Select(e => e.Description)));

            await transaccion.CommitAsync();
        }
        catch (Exception ex)
        {
            // Sin Commit → la transacción se revierte al disposal: ni el
            // usuario Identity ni el perfil quedan parciales.
            _logger.LogError(ex,
                "Fallo al crear el perfil de dominio o el rol Practicante (IdentityId={IdentityId}); se revirtió la operación completa.",
                identity.Id);
            TempData["Error"] = "Cuenta.Mensaje.ErrorCrearCuenta";
            return View(model);
        }

        // Sin PII en logs (ni email ni contraseñas).
        _logger.LogInformation("Cuenta de practicante creada (IdentityId={IdentityId}, PerfilId={PerfilId}).",
            identity.Id, identity.DomainUserId);

        TempData["Exito"] = "Cuenta.Mensaje.CuentaCreada";
        return RedirectToAction(nameof(Login));
    }

    // ── Perfil ─────────────────────────────────────────────────────────────

    /// <summary>
    /// «Mi perfil»: única pantalla de perfil del practicante.
    ///
    /// Muestra la foto de perfil (120×120, esquinas redondeadas; avatar con
    /// iniciales si no hay foto), el nombre y el cinturón (editables), y
    /// mantiene visibles email, rol y fecha de registro.
    ///
    /// Exclusivo para sesiones Identity autenticadas (misma política que el
    /// Guía); un desafío redirige al login público.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = IdentitySetup.PoliticaUsuarioAutenticado)]
    public async Task<IActionResult> Perfil()
    {
        var email = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
            return Challenge();

        var perfil = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);

        if (perfil is null)
            return NotFound();

        var foto = await _fotos.ObtenerFotoAsync(perfil.Id);
        var model = PerfilViewModel.Desde(perfil, foto is null ? null : IPerfilFotoService.RutaPublica(foto.MediaAssetId));

        return View(model);
    }

    /// <summary>
    /// Guarda los cambios de «Mi perfil»: nombre, cinturón (siempre un valor
    /// del catálogo) y, si se eligió un archivo, la fotografía de perfil.
    ///
    /// Seguridad:
    /// - Solo se modifica el perfil del usuario autenticado (nunca otro).
    /// - El cinturón se valida contra el catálogo real: no se acepta un valor
    ///   libre y el ROL nunca se toca (el cinturón es dato de perfil).
    /// - La foto se valida en el servidor (tipo, tamaño y contenido real) antes
    ///   de almacenarse; se reutiliza el modelo UserProfileMedia/MediaAsset.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = IdentitySetup.PoliticaUsuarioAutenticado)]
    public async Task<IActionResult> Perfil(PerfilViewModel model, IFormFile? foto)
    {
        var email = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
            return Challenge();

        var perfil = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (perfil is null)
            return NotFound();

        // ── Validaciones de servidor ──
        if (string.IsNullOrWhiteSpace(model.Nombre))
            ModelState.AddModelError(nameof(PerfilViewModel.Nombre), _localizador["Validacion.Nombre.Requerido"]);

        var opcionCinturon = CinturonVisual.OpcionesRegistro.FirstOrDefault(o =>
            string.Equals(o.Valor, (model.Cinturon ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
        if (opcionCinturon is null)
            ModelState.AddModelError(nameof(PerfilViewModel.Cinturon), _localizador["Validacion.Cinturon.Seleccion"]);

        bool hayArchivo = foto is not null && foto.Length > 0;
        if (hayArchivo)
        {
            var validacion = PerfilFotoValidador.Validar(foto);
            if (!validacion.EsValido)
                ModelState.AddModelError("foto", validacion.Error ?? _localizador["Cuenta.Mensaje.FotoInvalida"]);
        }

        if (!ModelState.IsValid)
        {
            var fotoVigente = await _fotos.ObtenerFotoAsync(perfil.Id);
            CompletarVista(model, perfil, fotoVigente);
            return View(model);
        }

        // ── Nombre y cinturón (dato de perfil; el rol NO se modifica) ──
        perfil.Name = model.Nombre.Trim();
        perfil.BeltRank = opcionCinturon!.Valor;
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Perfil de práctico actualizado (PerfilId={PerfilId}, Cinturon={Cinturon}).",
            perfil.Id, perfil.BeltRank);

        // ── Fotografía (opcional) ──
        bool fotoGuardada = true;
        if (hayArchivo)
        {
            var resultado = await _fotos.GuardarAsync(perfil.Id, foto);
            fotoGuardada = resultado.Exito;
            if (!fotoGuardada)
                TempData["Error"] = resultado.Error;
        }

        // NOTE: TempData se serializa en cookies: solo admite texto plano
        // (un LocalizedString provokeba HTTP 500 al guardar la respuesta).
        TempData["Exito"] = fotoGuardada
            ? "Cuenta.Mensaje.CambiosGuardados"
            : "Cuenta.Mensaje.CambiosGuardadosSinFoto";

        return RedirectToAction(nameof(Perfil));
    }

    /// <summary>
    /// Sirve la fotografía de perfil con control de acceso: solo la descarga
    /// el dueño del perfil. El archivo NUNCA se expone como recurso estático.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = IdentitySetup.PoliticaUsuarioAutenticado)]
    public async Task<IActionResult> FotoPerfil(int id)
    {
        var email = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
            return Challenge();

        var perfil = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email);
        if (perfil is null)
            return NotFound();

        var foto = await _fotos.ObtenerFotoPorRecursoAsync(id, perfil.Id);
        if (foto is null)
            return NotFound();

        var ruta = _fotos.ResolverRutaAbsoluta(foto.RutaRelativa);
        if (ruta is null || !System.IO.File.Exists(ruta))
            return NotFound();

        Response.Headers.CacheControl = "private, max-age=300";
        return PhysicalFile(ruta, foto.MimeType, enableRangeProcessing: true);
    }

    // ── Editar perfil (ruta legacy) ─────────────────────────────────────

    /// <summary>
    /// La edición de perfil vive ahora en «Mi perfil» (única pantalla):
    /// esta ruta antigua redirige allí en lugar de duplicar la functionality.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = IdentitySetup.PoliticaUsuarioAutenticado)]
    public IActionResult EditarPerfil() => RedirectToAction(nameof(Perfil));

    /// <summary>
    /// Completa el modelo con los datos de solo lectura (email, rol, fecha,
    /// foto vigente) cuando el formulario se vuelve a mostrar con errores.
    /// </summary>
    private void CompletarVista(PerfilViewModel model, User perfil, FotoPerfilInfo? fotoVigente)
    {
        model.Email = perfil.Email;
        model.Rol = perfil.Role.ToString();
        model.MiembroDesde = perfil.CreatedAt;
        model.FotoUrl = fotoVigente is null ? null : IPerfilFotoService.RutaPublica(fotoVigente.MediaAssetId);
        model.Iniciales = PerfilViewModel.InicialesDe(model.Nombre);
        model.OpcionesCinturon = CinturonVisual.OpcionesRegistro;
    }

    // ── Configuración ────────────────────────────────────────────────────

    /// <summary>
    /// Preferencias del usuario: hoy, el IDIOMA de la aplicación.
    ///
    /// No repite los datos de la cuenta (nombre, email, rol y cinturón): esos
    /// se administran desde «Mi perfil». La preferencia de idioma se guarda
    /// en <c>UserSettings.Language</c> (columna ya existente: sin migración)
    /// y además en una cookie, para que las visitas anónimas respeten el
    /// idioma elegido.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = IdentitySetup.PoliticaUsuarioAutenticado)]
    public async Task<IActionResult> Configuracion()
    {
        var email = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
            return Challenge();

        var perfil = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);

        if (perfil is null)
            return NotFound();

        var idiomaActual = await _preferenciasIdioma.ObtenerAsync(perfil.Id) ?? Idiomas.Es;

        return View(ConstruirVistaConfiguracion(idiomaActual));
    }

    /// <summary>
    /// Guarda la preferencia de idioma del practicante.
    ///
    /// - El código se valida contra la lista real de idiomas soportados
    ///   (nunca se guarda un idioma que la aplicación no sepa aplicar).
    /// - Se persiste en <c>UserSettings.Language</c>: sobrevive a cerrar
    ///   sesión, recargar y volver a ingresar.
    /// - Se escribe la cookie de idioma para las visitas anónimas y como
    ///   respaldo rápido. NO se borra al cerrar sesión.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = IdentitySetup.PoliticaUsuarioAutenticado)]
    public async Task<IActionResult> Configuracion(ConfiguracionViewModel model)
    {
        var email = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
            return Challenge();

        var perfil = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);

        if (perfil is null)
            return NotFound();

        var idiomaGuardado = await _preferenciasIdioma.ObtenerAsync(perfil.Id) ?? Idiomas.Es;

        // Validación en el SERVIDOR: el código se contrasta con la lista real
        // de idiomas soportados. NO se confía en el ModelState del enlazado
        // (un cliente podría omitirlo): si el idioma no existe, se rechaza.
        var codigo = Idiomas.Normalizar(model.Idioma);
        if (codigo is null
            && !ModelState.ContainsKey(nameof(ConfiguracionViewModel.Idioma)))
        {
            ModelState.AddModelError(nameof(ConfiguracionViewModel.Idioma),
                _localizador["Validacion.Idioma.Soportado"]);
        }

        if (!ModelState.IsValid)
        {
            var vistaInvalida = ConstruirVistaConfiguracion(idiomaGuardado);
            vistaInvalida.Idioma = model.Idioma;
            return View(vistaInvalida);
        }

        codigo ??= idiomaGuardado;

        try
        {
            await _preferenciasIdioma.GuardarAsync(perfil.Id, codigo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo guardar la preferencia de idioma (PerfilId={PerfilId}).", perfil.Id);
            ModelState.AddModelError(string.Empty, _localizador["Cuenta.Configuracion.Error"]);
            return View(ConstruirVistaConfiguracion(idiomaGuardado));
        }

        // Cookie: mantiene la preferencia fuera de la sesión (páginas públicas)
        // y como respaldo rápido. No se borra al cerrar sesión.
        Response.Cookies.Append(Idiomas.NombreCookie, codigo, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            HttpOnly = false,
            Path = "/"
        });

        _logger.LogInformation("Preferencia de idioma actualizada (PerfilId={PerfilId}, Idioma={Idioma}).",
            perfil.Id, codigo);
        // La cultura de esta petición era la ANTERIOR (la que estaba
        // persistida): se cambia ahora para que la respuesta del redirect ya
        // hable el idioma NUEVO. El mensaje de confirmación se guarda como
        // CLAVE y se traduce al pintar (ver Mensajes.Traducir): así sale en el
        // idioma con el que el usuario va a ver la pantalla.
        LocalizationSetup.AplicarCultura(codigo);
        TempData["Exito"] = "Cuenta.Configuracion.Exito";
        return RedirectToAction(nameof(Configuracion));
    }

    /// <summary>
    /// Arma el modelo de la pantalla de Configuración con el catálogo de
    /// idiomas (nombres ya traducidos al idioma de la petición) y el idioma
    /// vigente.
    /// </summary>
    private ConfiguracionViewModel ConstruirVistaConfiguracion(string idiomaActual)
    {
        var opciones = Idiomas.Opciones(_localizador);
        var nombre = opciones.FirstOrDefault(o => o.Codigo == idiomaActual)?.Nombre
                     ?? opciones[0].Nombre;

        return new ConfiguracionViewModel
        {
            Idioma = idiomaActual,
            IdiomaActual = idiomaActual,
            IdiomaActualNombre = nombre,
            Idiomas = opciones
        };
    }

    // ── Login ─────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Login(string? returnUrl) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            // Mismo mensaje que una contraseña incorrecta: sin enumeración.
            ModelState.AddModelError(string.Empty, _localizador[CredencialesInvalidasMensaje]);
            return View(model);
        }

        var resultado = await _signInManager.PasswordSignInAsync(
            user, model.Password, isPersistent: false, lockoutOnFailure: true);

        if (resultado.Succeeded)
        {
            _logger.LogInformation("Sesión de práctico iniciada (IdentityId={UserId}).", user.Id);

            // Feedback visual de login exitoso (Etapa UI/UX): el layout emite
            // un toast efímero en la página de destino. TempData dura una
            // petición y no lleva datos sensibles.
            TempData["Bienvenida"] = "Cuenta.Mensaje.Bienvenida";

            return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) && !string.IsNullOrWhiteSpace(model.ReturnUrl)
                ? model.ReturnUrl
                : "/");
        }

        if (resultado.IsLockedOut)
        {
            _logger.LogWarning("Cuenta bloqueada por intentos fallidos (IdentityId={UserId}).", user.Id);
            ModelState.AddModelError(string.Empty,
                _localizador["Cuenta.Mensaje.Bloqueada"]);
            return View(model);
        }

        ModelState.AddModelError(string.Empty, _localizador[CredencialesInvalidasMensaje]);
        return View(model);
    }

    // ── Logout ────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // Cierra SOLO la sesión de Identity (esquema Identity.Application):
        // la cookie administrativa de Development no se toca.
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // ── Acceso denegado ───────────────────────────────────────────────────

    /// <summary>
    /// Página local de "acceso denegado" (Etapa 5): destino de
    /// <c>AccessDeniedPath</c> cuando la sesión es VÁLIDA pero no tiene el rol
    /// requerido (p. ej. un práctico en el panel administrativo). Sin datos
    /// sensibles y sin parámetros utilizables para redirigir a sitios externos.
    /// </summary>
    [HttpGet]
    public IActionResult AccesoDenegado() => View();

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Mapea errores de Identity a ModelState sin exponer detalles sensibles.</summary>
    private void AgregarErroresIdentity(IEnumerable<IdentityError> errores, bool esRegistro)
    {
        foreach (var error in errores)
        {
            if (error.Code is "DuplicateUserName" or "DuplicateEmail")
            {
                ModelState.AddModelError(nameof(RegistroViewModel.Email), _localizador["Cuenta.Mensaje.EmailExistente"]);
            }
            else if (error.Code.StartsWith("Password", StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(RegistroViewModel.Password),
                    _localizador["Cuenta.Mensaje.PasswordInvalida"]);
            }
            else if (error.Code == "InvalidEmail")
            {
                ModelState.AddModelError(nameof(RegistroViewModel.Email), _localizador["Cuenta.Mensaje.EmailInvalido"]);
            }
            else
            {
                ModelState.AddModelError(string.Empty, _localizador["Cuenta.Mensaje.ErrorDatos"]);
            }
        }
    }
}
