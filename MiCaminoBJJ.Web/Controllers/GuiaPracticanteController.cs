using MiCaminoBJJ.Web.Localization;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Security;
using MiCaminoBJJ.Web.Services.Guia;
using MiCaminoBJJ.Web.ViewModels.Guia;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace MiCaminoBJJ.Web.Controllers;

/// <summary>
/// Guía del Practicante (Etapa 5): agente conversacional MVP que acompaña el
/// aprendizaje de BJJ usando la biblioteca de técnicas de SQL Server.
///
/// Seguridad:
/// - Exclusivo para sesiones de Identity autenticadas (política
///   <see cref="IdentitySetup.PoliticaUsuarioAutenticado"/>); el desafío
///   redirige al login público.
/// - Antiforgery explícito en el POST (el navegador envía el token por header).
/// - El único dato de perfil que se consulta es el nivel de cinturón del
///   MISMO usuario autenticado (AsNoTracking, sin email hacia el proveedor).
/// - La clave del proveedor de IA jamás pasa por esta capa: vive en el
///   servidor (User Secrets) y solo se usa dentro de <see cref="ProveedorIaCliente"/>.
/// - Límites de longitud validados en el servidor (no dependen del cliente).
/// </summary>
[Authorize(Policy = IdentitySetup.PoliticaUsuarioAutenticado)]
public class GuiaPracticanteController : Controller
{
    private readonly IGuiaPracticanteService _guia;
    private readonly WebTfDbContext _db;
    private readonly IStringLocalizer<UiResources> _localizador;
    private readonly ILogger<GuiaPracticanteController> _logger;

    public GuiaPracticanteController(
        IGuiaPracticanteService guia,
        WebTfDbContext db,
        IStringLocalizer<UiResources> localizador,
        ILogger<GuiaPracticanteController> logger)
    {
        _guia = guia ?? throw new ArgumentNullException(nameof(guia));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _localizador = localizador ?? throw new ArgumentNullException(nameof(localizador));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Pantalla de conversación (con bienvenida y sugerencias).</summary>
    [HttpGet]
    public IActionResult Index() => View(GuiaChatViewModel.Inicial(_guia.ModoDescripcion, _localizador));

    /// <summary>
    /// Endpoint JSON que procesa una pregunta. Siempre responde con el objeto
    /// de respuesta (incluso los errores), para que la interfaz muestre
    /// mensajes claros.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Consultar([FromBody] GuiaConsultaViewModel? model, CancellationToken cancelacion)
    {
        if (model is null || !ModelState.IsValid)
        {
            var mensaje = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e))
                ?? "La pregunta no es válida.";

            return BadRequest(GuiaRespuestaViewModel.Error(mensaje));
        }

        try
        {
            var nivel = await ObtenerNivelCinturonAsync();
            var respuesta = await _guia.ResponderAsync(
                new GuiaConsulta(model.Texto, nivel), cancelacion);

            return Ok(GuiaRespuestaViewModel.Desde(respuesta));
        }
        catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
        {
            // El navegador se desconectó: no hay a quien responder.
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            // Sin volcar detalles internos ni datos al cliente.
            _logger.LogError(ex, "Guía: error inesperado al procesar la consulta.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                GuiaRespuestaViewModel.Error(
                    "No se pudo procesar la consulta. Intentá nuevamente en unos minutos."));
        }
    }

    /// <summary>
    /// Nivel de cinturón del USUARIO AUTENTICADO (único dato de perfil
    /// utilizado; nunca se consulta ni expone información de otros usuarios).
    /// </summary>
    private async Task<string?> ObtenerNivelCinturonAsync()
    {
        var email = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var nivel = await _db.Users.AsNoTracking()
            .Where(u => u.Email == email)
            .Select(u => u.BeltRank)
            .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(nivel) ? null : nivel;
    }
}
