namespace MiCaminoBJJ.Web.Services.Guia;

/// <summary>
/// Configuración del proveedor de IA del guía (sección "GuiaPracticante").
///
/// SEGURIDAD: la clave NO vive en el repositorio. Se configura SOLO en
/// Development con User Secrets:
/// <code>
///   dotnet user-secrets set "GuiaPracticante:ApiKey"   "&lt;clave&gt;"
///   dotnet user-secrets set "GuiaPracticante:Endpoint" "&lt;url del chat/completions&gt;"
///   dotnet user-secrets set "GuiaPracticante:Modelo"   "&lt;modelo&gt;"
/// </code>
/// Sin las tres claves el guía opera en modo CATÁLOGO (etiquetado como tal en
/// la interfaz); nunca se inventa un endpoint ni una clave.
/// </summary>
public sealed class GuiaOpciones
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "GuiaPracticante";

    /// <summary>Endpoint del proveedor compatible con chat/completions.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Clave del proveedor (NUNCA se registra en logs ni se envía al navegador).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Identificador del modelo a usar.</summary>
    public string Modelo { get; set; } = string.Empty;

    /// <summary>
    /// true solo si endpoint ABSOLUTO, clave y modelo están completos: recién
    /// entonces se intenta la ruta de IA (con fallback a catálogo ante fallos).
    /// </summary>
    public bool EstaConfigurada() =>
        !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(Modelo)
        && Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
