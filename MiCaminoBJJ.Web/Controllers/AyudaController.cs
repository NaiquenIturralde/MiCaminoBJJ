using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MiCaminoBJJ.Web.Controllers;

/// <summary>
/// Ayuda y manual de uso de Mi Camino BJJ (/Ayuda).
///
/// Pantalla informativa SOLO de lo que EXISTE hoy en la web:
/// biblioteca de técnicas, búsqueda por cinturón, reproducción de videos,
/// Guía del Practicante, administración del perfil y diferencias entre
/// Practicante y Administrador.
///
/// - No requiere sesión (se puede consultar antes de iniciar sesión).
/// - No modifica datos: no hay ModelState ni escrituras.
/// - No anuncia módulos futuros como disponibles.
/// </summary>
[AllowAnonymous]
public sealed class AyudaController : Controller
{
    /// <summary>Manual de uso.</summary>
    [HttpGet]
    public IActionResult Index() => View();
}
