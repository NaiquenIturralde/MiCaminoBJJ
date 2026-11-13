namespace MiCaminoBJJ.Web.ViewModels;

/// <summary>
/// Tarjeta de contenido de la página de inicio (Etapa 0 Web).
/// Presentación pura: no accede a la base de datos ni a servicios compartidos.
/// </summary>
public sealed class FeatureCardViewModel
{
    /// <summary>Emoji o glifo representativo.</summary>
    public string Icon { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    /// <summary>Etiqueta corta opcional, p. ej. "Disponible" o "Etapa 1.2".</summary>
    public string? Badge { get; init; }
}
