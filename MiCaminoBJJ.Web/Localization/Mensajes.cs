using Microsoft.Extensions.Localization;

namespace MiCaminoBJJ.Web.Localization;

/// <summary>
/// Utilidades para mostrar mensajes de sistema (TempData) en el idioma
/// CORRECTO.
///
/// El problema que resuelve: un mensaje guardado en TempData se escribe en una
/// petición y se LEE en otra (el redirect siguiente). Si se traducía al
/// escribirlo, el mensaje quedaría en el idioma que tenía la petición
/// emissora (por ejemplo, el login se hace en español y la página de destino
/// ya está en inglés).
///
/// Solución: en TempData se guarda la CLAVE del catálogo y el mensaje se
/// traduce al Pintar, cuando la cultura de la petición ya es la definitiva.
/// Un valor que NO sea una clave del catálogo se muestra tal cual (mensajes
/// que vienen de otra capa, como un error de validación de archivo).
/// </summary>
public static class Mensajes
{
    /// <summary>
    /// Traduce el mensaje si su valor es una clave del catálogo; si no lo es,
    /// lo devuelve sin cambios.
    /// </summary>
    public static string Traducir(IStringLocalizer<UiResources> localizador, string? valor)
    {
        ArgumentNullException.ThrowIfNull(localizador);

        if (string.IsNullOrEmpty(valor))
            return string.Empty;

        var localizada = localizador[valor];

        return localizada.ResourceNotFound ? valor : localizada.Value;
    }
}
