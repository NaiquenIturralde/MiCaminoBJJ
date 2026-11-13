using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Web.Localization;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Security;
using MiCaminoBJJ.Web.Services;
using MiCaminoBJJ.Web.Services.Admin;
using MiCaminoBJJ.Web.Services.Auditoria;
using MiCaminoBJJ.Web.Services.Guia;
using MiCaminoBJJ.Web.Services.Perfil;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ── Localización (es / en / pt) ────────────────────────────────────────────
// Se registra ANTES de los controladores para que AddViewLocalization y
// AddDataAnnotationsLocalization encuentren el IStringLocalizerFactory.
// - AddWebLocalization: IStringLocalizer + servicio de preferencia de idioma.
// - AddViewLocalization: IViewLocalizer para las vistas Razor.
// - AddDataAnnotationsLocalization: traduce los mensajes de validación de los
//   formularios usando el catálogo compartido (Localization.UiResources).
builder.Services.AddWebLocalization();

// Add services to the container.
// Etapa 3: AutoValidateAntiforgeryToken exige token en TODOS los POST/PUT/DELETE
// (los GET quedan exentos por definición). Además cada POST de los formularios
// declara [ValidateAntiForgeryToken] de forma explícita.
builder.Services.AddControllersWithViews(options =>
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
    {
        // Todos los mensajes de DataAnnotations salen del catálogo compartido:
        // el "ErrorMessage" del atributo es la CLAVE del recurso.
        options.DataAnnotationLocalizerProvider = (type, factory) => factory.Create(typeof(UiResources));
    });

// Servicios web de presentación (Etapa 0).
// La infraestructura compartida (EF Core + SQLite) todavía NO se registra aquí:
// esta etapa no toca la base, no ejecuta migraciones ni usa EnsureCreated/EnsureDeleted.
builder.Services.AddSingleton<IHomeContentService, HomeContentService>();

// Biblioteca de técnicas (Etapa 2B): consulta REAL sobre SQL Server
// (WebTfDbContext, solo lectura con AsNoTracking). Reemplaza a
// TecnicasDemoConsultaService SIN tocar controladores ni vistas: la insignia
// "Datos de demostración" desaparece porque el nuevo servicio declara
// EsFuenteDeDemostracion = false. Ante un error de conexión el controlador
// registra el fallo y muestra su mensaje de error: NO hay fallback a datos
// en memoria (mostrarlos como reales sería incorrecto).
builder.Services.AddScoped<ITecnicasConsultaService, TecnicasSqlServerConsultaService>();

// Persistencia SQL Server de la web (Etapa 2A): PROVIDER preparado y aislado.
// No registra AddInfrastructure (SQLite de MAUI queda intacto).
builder.Services.AddWebTfPersistence(builder.Configuration);

// ── Etapa 4B: ASP.NET Core Identity (SOLO infraestructura; sin pantallas) ──
// ORDEN DELICADO: Identity se registra ANTES del esquema admin de abajo.
// La AddAuthentication del admin fija DefaultAuthenticateScheme = "Cookies"
// como último configurador, de modo que cualquier [Authorize] "desnudo" sigue
// resolviendo contra el esquema ADMIN (fail-closed): una futura cookie de
// Identity no autoriza rutas sin política, y el CRUD usa la política
// AdminLocal (esquema "Cookies" + rol Administrador) donde una identidad de
// practicante jamás entra.
builder.Services.AddWebIdentity();

// ── Etapa 3/4B: acceso administrativo LOCAL (SOLO Development) ──
// La configuración vive en AdminAuthSetup (movida sin cambios desde aquí para
// poder probarla): cookie "MiCaminoBJJ.Admin" del esquema "Cookies", credencial
// en User Secrets, 404 fuera de Development (AdminSoloDevelopment) y política
// AdminLocal (esquema admin + rol Administrador) para el CRUD.
// ORDEN IMPORTANTE: después de AddWebIdentity para que la AddAuthentication de
// aquí fije DefaultAuthenticateScheme = "Cookies" como último configurador
// (cualquier [Authorize] "desnudo" cae en el esquema admin: fail-closed).
builder.Services.AddAdminAuth();

// ── Etapa 3: CRUD administrativo de técnicas ──
// Auditoría con el CONTRATO compartido IAuditService, adaptado a WebTfDbContext
// (mismo contexto que el comando → misma transacción). NO se registra
// AddInfrastructure: SQLite de MAUI queda intacta.
builder.Services.AddScoped<IAuditService, WebAuditService>();
builder.Services.AddScoped<IAdminTecnicasQueryService, AdminTecnicasQueryService>();
builder.Services.AddScoped<IAdminTecnicasCommandService, AdminTecnicasCommandService>();

// ── Auditoría (pantalla del administrador + reporte PDF descargable) ──
// SOLO LECTURA sobre la tabla AuditLogs ya existente (no hay tablas nuevas
// ni migraciones) y generación del PDF con QuestPDF (licencia Community).
builder.Services.AddScoped<IAuditoriaConsultaService, AuditoriaConsultaService>();
builder.Services.AddScoped<IAuditoriaPdfService, AuditoriaPdfService>();

// ── Multimedia de técnicas (sección de Editar) ──
// Parser compartido de URLs de video (YouTube/Vimeo): estado puro de Core,
// singleton. NO se registra AddInfrastructure (SQLite de MAUI queda intacta).
builder.Services.AddSingleton<IExternalMediaParser, ExternalMediaParser>();
builder.Services.AddScoped<IAdminTecnicasMediaQueryService, AdminTecnicasMediaQueryService>();
builder.Services.AddScoped<IAdminTecnicasMediaCommandService, AdminTecnicasMediaCommandService>();
builder.Services.AddScoped<IDevAdminCredentialValidator, DevAdminCredentialValidator>();
builder.Services.AddScoped<IDevAdminSession, DevAdminSession>();

// ── Etapa 5: Guía del Practicante (agente conversacional MVP) ──
// El proveedor de IA se configura SOLO con User Secrets en Development:
//   dotnet user-secrets set "GuiaPracticante:ApiKey"   "<clave>"
//   dotnet user-secrets set "GuiaPracticante:Endpoint" "<url chat/completions>"
//   dotnet user-secrets set "GuiaPracticante:Modelo"   "<modelo>"
// Sin esas claves el guía opera en modo CATÁLOGO (etiquetado en la interfaz
// como "guía basada en catálogo"). La clave NUNCA se versiona ni se expone
// al navegador: solo la consume ProveedorIaCliente desde el servidor.
builder.Services.Configure<GuiaOpciones>(builder.Configuration.GetSection(GuiaOpciones.Seccion));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<GuiaOpciones>>().Value);
builder.Services.AddScoped<IGuiaPracticanteService, GuiaPracticanteService>();
builder.Services.AddHttpClient<IProveedorIaCliente, ProveedorIaCliente>(cliente =>
{
    cliente.Timeout = GuiaLimites.TimeoutHttp;
});

// ── Mi perfil: fotografía del practicante ──
// Reutiliza el modelo existente (MediaAsset + UserProfileMedia): NO hay
// tablas nuevas ni migraciones. Las imágenes se validan en el servidor (tipo,
// tamaño y contenido real), se guardan FUERA de wwwroot y se sirven por una
// acción del controlador con control de acceso (solo su dueño las ve).
builder.Services.Configure<PerfilFotoOpciones>(builder.Configuration.GetSection(PerfilFotoOpciones.Seccion));
builder.Services.PostConfigure<PerfilFotoOpciones>(opciones =>
{
    if (string.IsNullOrWhiteSpace(opciones.Ruta))
        opciones.Ruta = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "fotos-perfil");
});
builder.Services.AddScoped<IPerfilFotoService, PerfilFotoService>();

var app = builder.Build();

// ── Etapa 4D-B: comando local init-admin ──
// Ejecución explícita: dotnet run --project MiCaminoBJJ.Web -- init-admin
// NO arranca Kestrel ni ejecuta seeders: resuelve servicios scoped con CreateScope()
// para UserManager/RoleManager/WebTfDbContext/WebAuditService.
if (AdminBootstrap.EsComando(args))
{
    using var scopeInitAdmin = app.Services.CreateScope();
    Environment.ExitCode = await AdminBootstrap.EjecutarAsync(scopeInitAdmin.ServiceProvider, app.Environment, args);
    return;
}

// ── Etapa 4D-C: comando local reset-admin-password ──
// Restablece la contraseña del administrador EXISTENTE usando la API oficial de
// Identity (GeneratePasswordResetToken + ResetPassword). No crea cuentas ni
// roles, no ejecuta init-admin, no migra y no toca otros usuarios: verifica el
// vínculo DomainUserId y la vigencia del rol Administrador ANTES de escribir.
// Las guardas (solo Development + base exacta + candado + respaldo verificado)
// replican el patrón ya auditado de init-admin.
if (ResetAdminPassword.EsComando(args))
{
    using var scopeResetAdmin = app.Services.CreateScope();
    Environment.ExitCode = await ResetAdminPassword.EjecutarAsync(scopeResetAdmin.ServiceProvider, app.Environment, args);
    return;
}

// ── Etapa 4: cabeceras de seguridad en TODAS las respuestas ──
// Protección anti-clickjacking (X-Frame-Options: DENY) sobre todo el sitio,
// con foco en el panel administrativo; nosniff y Referrer-Policy acotados.
// Corrección de hallazgo de auditoría, con prueba de regresión en Web.Tests.
app.Use(async (context, next) =>
{
    WebSecurityHeaders.Apply(context.Response.Headers);
    await next();
});

// Etapa 2B: carga inicial idempotente de la biblioteca. SOLO en Development y
// SOLO si la base ya fue migrada (Fase 2, dotnet ef database update). Si algo
// falla se registra el error y la app sigue arrancando: la biblioteca mostrará
// su mensaje de error en lugar de datos falsos.
if (app.Environment.IsDevelopment())
{
    await SembrarBibliotecaAsync(app);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// ── Etapa 3/4B/4C.1: autenticación y autorización ──
// Esquemas: "Cookies" (admin local de Development, cookie MiCaminoBJJ.Admin) e
// "Identity.Application" (prácticos, cookie MiCaminoBJJ.Cuenta). Con Identity
// registrado, los defaults quedan en "Identity.Application" (comportamiento
// ESTÁNDAR del framework; verificado por prueba). El AISLAMIENTO del CRUD no
// depende de los defaults: la política AdminLocal está anclada al esquema
// "Cookies" y el AuthorizationMiddleware autentica ese esquema EXPLÍCITAMENTE,
// por lo que una cookie de Identity jamás autoriza el administrador. La
// política PracticanteAutenticado ancla lo propio al esquema Identity. No
// existe ningún [Authorize] sin política en el proyecto (verificado por
// reflexión). Etapa 4C.1: se eliminó el middleware que mutaba HttpContext.User
// y el PostConfigure de defaults (ver informe).
app.UseAuthentication();

// ── Localización de la petición ───────────────────────────────────────────
// UseRequestLocalization (mecanismo estándar de ASP.NET Core) resuelve la
// cultura por cookie → cabecera Accept-Language → español por defecto.
// Va DESPUÉS de la autenticación para que el middleware de preferencia
// persistida (que necesita HttpContext.User) pueda aplicar el idioma
// guardado en UserSettings.Language, que es la fuente de verdad.
app.UseRequestLocalization(LocalizationSetup.CrearOpcionesRequestLocalization());
app.UsePreferenciaIdioma();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

// Carga inicial idempotente del catálogo de la biblioteca (Etapa 2B).
// Falla visible en el log si la base no está migrada/disponible; nunca
// silencia el error ni degrada a datos de demostración.
static async Task SembrarBibliotecaAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<WebTfDbContext>();
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("WebLibrarySeeder");

    try
    {
        await WebLibrarySeeder.SeedAsync(db, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(
            ex,
            "No se pudo completar la carga inicial de la biblioteca web. "
            + "Verificá que exista la base MiCaminoBJJ_Web_TFI_2026 y que esté migrada "
            + "(dotnet ef database update). La aplicación continúa; la biblioteca "
            + "mostrará su mensaje de error.");
    }
}

/// <summary>
/// Marcador público para <c>WebApplicationFactory&lt;Program&gt;</c> (Etapa 4C.1):
/// las pruebas de SEGURIDAD del pipeline HTTP arrancan la aplicación real en
/// memoria (cookies, antiforgery, políticas y gate de Development reales).
/// </summary>
public partial class Program
{
}
