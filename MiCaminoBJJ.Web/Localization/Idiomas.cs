using System.Globalization;
using Microsoft.Extensions.Localization;

namespace MiCaminoBJJ.Web.Localization;

/// <summary>
/// Un idioma disponible en la pantalla de Configuración.
/// </summary>
/// <param name="Codigo">Código ISO 639-1 ("es", "en" o "pt").</param>
/// <param name="Nombre">Nombre del idioma en el idioma actual (traducido).</param>
public sealed record IdiomaOpcion(string Codigo, string Nombre);

/// <summary>
/// Catálogo de idiomas soportados por la web: Español (es), English (en) y
/// Português (pt).
///
/// Es la ÚNICA fuente de verdad sobre los códigos válidos: la vista de
/// Configuración, el POST de guardado, el proveedor de cultura y la
/// persistencia usan todos esta lista, de modo que no puede haber un idioma
/// "guardado" que la aplicación no sepa aplicar.
///
/// Los nombres se traducen con el localizador de recursos; los CÓDIGOS no se
/// traducen (son identificadores técnicos, igual que un nombre de archivo).
/// </summary>
public static class Idiomas
{
    /// <summary>Código ISO 639-1 de español.</summary>
    public const string Es = "es";

    /// <summary>Código ISO 639-1 de inglés.</summary>
    public const string En = "en";

    /// <summary>Código ISO 639-1 de portugués.</summary>
    public const string Pt = "pt";

    /// <summary>
    /// Nombre de la cookie que transporta la preferencia de idioma para las
    /// visitas anónimas (y como respaldo rápido de la preferencia persistida).
    /// </summary>
    public const string NombreCookie = "MiCaminoBJJ.Idioma";

    /// <summary>Códigos soportados, en el orden en que se muestran.</summary>
    public static readonly IReadOnlyList<string> Codigos = [Es, En, Pt];

    /// <summary>Culturas soportadas (se registran en RequestLocalizationOptions).</summary>
    public static readonly IReadOnlyList<CultureInfo> Culturas =
    [
        new CultureInfo(Es),
        new CultureInfo(En),
        new CultureInfo(Pt)
    ];

    /// <summary>Cultura por defecto de la web cuando no hay ninguna preferencia.</summary>
    public static CultureInfo CulturaPorDefecto => new(Es);

    /// <summary>
    /// Indica si el código corresponde a un idioma soportado (comparación
    /// sin distinguir mayúsculas y espacios).
    /// </summary>
    public static bool EsValido(string? codigo) => Normalizar(codigo) is not null;

    /// <summary>
    /// Normaliza un código de idioma al formato soportado: acepta "es",
    /// "ES", "es-ES", "es_AR" y devuelve "es". Devuelve null cuando el idioma
    /// no está soportado (o el valor es vacío).
    /// </summary>
    public static string? Normalizar(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return null;

        var recorte = codigo.Trim();
        var separador = recorte.IndexOfAny(['-', '_']);
        if (separador > 0)
            recorte = recorte[..separador];

        recorte = recorte.ToLowerInvariant();

        return Codigos.Contains(recorte, StringComparer.Ordinal) ? recorte : null;
    }

    /// <summary>
    /// Devuelve la cultura para el código dado, o la cultura por defecto si
    /// el código no está soportado.
    /// </summary>
    public static CultureInfo CulturaDe(string? codigo)
    {
        var normalizado = Normalizar(codigo);
        return normalizado is null ? CulturaPorDefecto : new CultureInfo(normalizado);
    }

    /// <summary>
    /// Catálogo de idiomas con el nombre ya TRADUCIDO al idioma actual, en el
    /// orden en que se muestran en la pantalla de Configuración.
    ///
    /// El CÓDIGO no se traduce (es un identificador técnico); el nombre sí
    /// ("Español", "English", "Português").
    /// </summary>
    public static IReadOnlyList<IdiomaOpcion> Opciones(IStringLocalizer<UiResources> localizador)
    {
        ArgumentNullException.ThrowIfNull(localizador);

        return
        [
            new IdiomaOpcion(Es, localizador["Cuenta.Idioma.Es"]),
            new IdiomaOpcion(En, localizador["Cuenta.Idioma.En"]),
            new IdiomaOpcion(Pt, localizador["Cuenta.Idioma.Pt"])
        ];
    }
}
