namespace MiCaminoBJJ.Web.ViewModels;

/// <summary>
/// ViewModel de la página de inicio (Etapa 0 Web).
/// Concentra el contenido de presentación del landing: identidad visual,
/// funcionalidades y pilares de la arquitectura.
/// </summary>
public sealed class HomeIndexViewModel
{
    public string Title { get; init; } = "Mi Camino BJJ";

    /// <summary>Texto pequeño superior (cejilla) del hero.</summary>
    public string Eyebrow { get; init; } = string.Empty;

    /// <summary>Frase principal junto al título.</summary>
    public string Tagline { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    /// <summary>Grid de funcionalidades.</summary>
    public IReadOnlyList<FeatureCardViewModel> Features { get; init; } = Array.Empty<FeatureCardViewModel>();

    /// <summary>Grid de pilares de la arquitectura (móvil, web, núcleo).</summary>
    public IReadOnlyList<FeatureCardViewModel> Pillars { get; init; } = Array.Empty<FeatureCardViewModel>();
}
