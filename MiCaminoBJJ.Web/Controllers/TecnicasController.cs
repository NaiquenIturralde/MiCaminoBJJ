using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Web.Models;
using MiCaminoBJJ.Web.Services;
using MiCaminoBJJ.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace MiCaminoBJJ.Web.Controllers;

/// <summary>
/// Biblioteca de técnicas (Etapa 1 Web): solo lectura, GET exclusivamente.
///
/// Rutas (convención por defecto del pipeline):
/// - /Tecnicas
/// - /Tecnicas/Details/{id}   → HTTP 404 si el id no existe o la técnica está inactiva.
///
/// La fuente de datos llega por DI (<see cref="ITecnicasConsultaService"/>): hoy es
/// una demo en memoria y más adelante podrá ser SQL Server sin tocar este controlador.
/// </summary>
public class TecnicasController : Controller
{
    private readonly ITecnicasConsultaService _consulta;
    private readonly ILogger<TecnicasController> _logger;

    public TecnicasController(ITecnicasConsultaService consulta, ILogger<TecnicasController> logger)
    {
        _consulta = consulta;
        _logger = logger;
    }

    /// <summary>Listado con búsqueda y filtros (todo por parámetros GET).</summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? buscar, int? cinturon, int? categoria, int? posicion)
    {
        var filtro = CrearFiltro(buscar, cinturon, categoria, posicion);

        var model = new TecnicasIndexViewModel
        {
            Filtro = filtro,
            EsFuenteDeDemostracion = _consulta.EsFuenteDeDemostracion
        };

        try
        {
            var tecnicas = await _consulta.SearchAsync(filtro.ToSearchFilter());
            model.Tecnicas = tecnicas.Select(TecnicaCardViewModel.Desde).ToList();

            model.Cinturones = (await _consulta.GetBeltsAsync())
                .Select(FiltroOpcionViewModel.DesdeBelt).ToList();
            model.Categorias = (await _consulta.GetCategoriesAsync())
                .Select(FiltroOpcionViewModel.DesdeCategoria).ToList();
            model.Posiciones = (await _consulta.GetPositionsAsync())
                .Select(FiltroOpcionViewModel.DesdePosicion).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la consulta de la biblioteca de técnicas.");
            model.Tecnicas = Array.Empty<TecnicaCardViewModel>();
            model.MensajeError = "No se pudo cargar la biblioteca de técnicas. Intentá nuevamente en unos minutos.";
        }

        return View(model);
    }

    /// <summary>Detalle de una técnica; HTTP 404 si no existe.</summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id, string? buscar, int? cinturon, int? categoria, int? posicion)
    {
        var filtro = CrearFiltro(buscar, cinturon, categoria, posicion);

        Technique? tecnica;
        try
        {
            tecnica = await _consulta.GetByIdAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la consulta de la técnica {TecnicaId}.", id);
            return View("Error", new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }

        if (tecnica is null || !tecnica.IsActive)
            return NotFound();

        return View(TecnicaDetalleViewModel.Desde(tecnica, filtro.ToRutaListado(), _consulta.EsFuenteDeDemostracion));
    }

    private static TecnicasFiltroViewModel CrearFiltro(string? buscar, int? cinturon, int? categoria, int? posicion) =>
        new()
        {
            Buscar = buscar,
            Cinturon = cinturon,
            Categoria = categoria,
            Posicion = posicion
        };
}
