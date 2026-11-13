using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Web.Services;
using MiCaminoBJJ.Web.Services.Recorrido;
using MiCaminoBJJ.Web.ViewModels.Recorrido;
using Microsoft.AspNetCore.Mvc;

namespace MiCaminoBJJ.Web.Controllers;

/// <summary>
/// Recorrido educativo por cinturón (Etapa 6A: CINTURÓN BLANCO).
///
/// Rutas explícitas (attribute routing) — GET exclusivo, sin escrituras:
/// - /recorrido/cinturon-blanco              → índice (encabezado + módulos + técnicas)
/// - /recorrido/cinturon-blanco/modulo/{slug} → detalle de módulo (404 si el slug no existe)
/// - /recorrido/cinturon-blanco/ficha/{slug}  → ficha técnica con video (404 si el slug no existe)
///
/// El contenido educativo vive en <see cref="CinturonBlancoCatalogo"/> (en
/// código: esta etapa NO necesita migración SQL Server). La ÚNICA lectura de
/// base es la biblioteca existente (ITecnicasConsultaService, solo lectura con
/// AsNoTracking) para listar las técnicas de cinturón blanco y enlazar las
/// técnicas relacionadas con las fichas. Nunca se crean, modifican ni
/// duplican entidades.
/// </summary>
public sealed class RecorridoController : Controller
{
    /// <summary>Cinturón principal del recorrido (nombre exacto del catálogo de dominio).</summary>
    private const string CinturonBlanco = "Blanco";

    private readonly ITecnicasConsultaService _consulta;
    private readonly ILogger<RecorridoController> _logger;

    public RecorridoController(ITecnicasConsultaService consulta, ILogger<RecorridoController> logger)
    {
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Índice del recorrido del cinturón blanco.</summary>
    [HttpGet("/recorrido/cinturon-blanco")]
    public async Task<IActionResult> Blanco()
    {
        var model = RecorridoBlancoViewModel.DesdeCatalogo();
        model.EsFuenteDeDemostracion = _consulta.EsFuenteDeDemostracion;

        try
        {
            var todas = await _consulta.SearchAsync(new TechniqueSearchFilter());
            model.Tecnicas = todas
                .Where(t => string.Equals(t.Belt?.Name, CinturonBlanco, StringComparison.OrdinalIgnoreCase))
                .Select(TecnicaRelacionadaViewModel.Desde)
                .ToList();
        }
        catch (Exception ex)
        {
            // Mismo criterio que TecnicasController: sin fallback a datos en
            // memoria; se registra el fallo y se muestra un mensaje honesto.
            _logger.LogError(ex, "Falló la consulta de técnicas para el recorrido cinturón blanco.");
            model.Tecnicas = Array.Empty<TecnicaRelacionadaViewModel>();
            model.MensajeTecnicas =
                "No se pudieron cargar las técnicas de la biblioteca en este momento. "
                + "El contenido educativo del recorrido sigue disponible.";
        }

        return View(model);
    }

    /// <summary>Detalle de un módulo educativo; HTTP 404 si el slug no existe.</summary>
    [HttpGet("/recorrido/cinturon-blanco/modulo/{slug}")]
    public IActionResult Modulo(string slug)
    {
        var modulo = CinturonBlancoCatalogo.ModuloPorSlug(slug);
        if (modulo is null)
            return NotFound();

        var fichasRelacionadas = modulo.FichasRelacionadas
            .Select(CinturonBlancoCatalogo.FichaPorSlug)
            .Where(ficha => ficha is not null)
            .Select(ficha => FichaResumenViewModel.Desde(ficha!))
            .ToList();

        var model = ModuloDetalleViewModel.Desde(
            modulo,
            CinturonBlancoCatalogo.ModuloAnterior(modulo),
            CinturonBlancoCatalogo.ModuloSiguiente(modulo),
            fichasRelacionadas);

        return View(model);
    }

    /// <summary>Ficha técnica con video integrado; HTTP 404 si el slug no existe.</summary>
    [HttpGet("/recorrido/cinturon-blanco/ficha/{slug}")]
    public async Task<IActionResult> Ficha(string slug)
    {
        var ficha = CinturonBlancoCatalogo.FichaPorSlug(slug);
        if (ficha is null)
            return NotFound();

        var modulo = CinturonBlancoCatalogo.Modulos.First(m => m.Id == ficha.ModuloId);

        Technique? relacionada = null;
        var bibliotecaNoDisponible = false;

        if (ficha.TecnicaRelacionada is not null)
        {
            try
            {
                relacionada = await ResolverTecnicaRelacionadaAsync(ficha.TecnicaRelacionada);
            }
            catch (Exception ex)
            {
                bibliotecaNoDisponible = true;
                _logger.LogError(
                    ex,
                    "No se pudo resolver la técnica relacionada '{Tecnica}' de la ficha {Slug}.",
                    ficha.TecnicaRelacionada,
                    ficha.Slug);
            }
        }

        var model = FichaDetalleViewModel.Desde(
            ficha,
            modulo,
            CinturonBlancoCatalogo.FichaAnterior(ficha),
            CinturonBlancoCatalogo.FichaSiguiente(ficha),
            relacionada,
            bibliotecaNoDisponible);

        return View(model);
    }

    /// <summary>
    /// Busca la técnica de la biblioteca cuyo nombre O ALIAS coincide
    /// EXACTAMENTE (tras normalizar) con el nombre referenciado por la ficha.
    /// Un "contains" solo no alcanza: podría traer técnicas con nombres
    /// parecidos. Tolerancia a la importación del PDF: renombrados (el nombre
    /// histórico queda como alias) y ocultos (IsActive = 0) siguen resolviendo.
    /// </summary>
    private Task<Technique?> ResolverTecnicaRelacionadaAsync(string nombre) =>
        _consulta.GetByNombreOAliasExactoAsync(nombre);
}
