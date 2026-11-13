using System.ComponentModel.DataAnnotations;
using MiCaminoBJJ.Web.Localization;
using Microsoft.Extensions.Localization;

namespace MiCaminoBJJ.Web.ViewModels;

/// <summary>
/// Modelo de la pantalla «Configuración» (/Cuenta/Configuracion).
///
/// La pantalla es una sección de PREFERENCIAS del usuario: hoy expone el
/// idioma de la aplicación. NO repite los datos de la cuenta (nombre, email,
/// rol y cinturón), que se administran desde «Mi perfil».
///
/// El idioma se envía como código ISO 639-1 ("es", "en" o "pt") y se valida
/// contra la lista real de idiomas soportados: el cliente jamás puede elegir
/// un idioma que la aplicación no sepa aplicar.
/// </summary>
public sealed class ConfiguracionViewModel
{
    /// <summary>Código ISO 639-1 del idioma elegido ("es", "en" o "pt").</summary>
    [Required(ErrorMessage = "Validacion.Idioma.Requerido")]
    [RegularExpression("^(es|en|pt)$", ErrorMessage = "Validacion.Idioma.Soportado")]
    public string Idioma { get; set; } = global::MiCaminoBJJ.Web.Localization.Idiomas.Es;

    /// <summary>Catálogo de idiomas disponibles con su nombre traducido.</summary>
    public IReadOnlyList<IdiomaOpcion> Idiomas { get; set; } = Array.Empty<IdiomaOpcion>();

    /// <summary>Idioma que está vigente (el recién guardado o el que ya tenía).</summary>
    public string IdiomaActual { get; set; } = global::MiCaminoBJJ.Web.Localization.Idiomas.Es;

    /// <summary>Nombre traduitido del idioma vigente (para mostrarlo en pantalla).</summary>
    public string IdiomaActualNombre { get; set; } = string.Empty;
}
