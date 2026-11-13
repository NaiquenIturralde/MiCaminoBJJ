namespace MiCaminoBJJ.Web.Localization;

/// <summary>
/// Marcador de los recursos de texto de la interfaz (es / en / pt).
///
/// Es una clase vacía a propósito: su único papel es darle a
/// <c>IStringLocalizer</c> un punto de anclaje estable para resolver el
/// archivo de recursos compartido
/// <c>Resources/Localization.UiResources.resx</c> (y sus variantes
/// <c>.en.resx</c> / <c>.pt.resx</c>), siguiendo la convención de
/// <c>ResourceManagerStringLocalizerFactory</c>.
///
/// Uso típico:
/// <code>
/// @inject IStringLocalizer&lt;UiResources&gt; L
/// &lt;h1&gt;@L["Layout.Nav.Inicio"]&lt;/h1&gt;
/// </code>
///
/// En los controladores se inyecta el mismo localizador para resolver los
/// mensajes de ModelState, TempData y validaciones de negocio.
/// </summary>
public sealed class UiResources
{
    /// <summary>
    /// Identificador estable usado por las pruebas para localizar el archivo
    /// de recursos sin depender del idioma actual.
    /// </summary>
    public const string NombreArchivo = "Localization.UiResources";
}
