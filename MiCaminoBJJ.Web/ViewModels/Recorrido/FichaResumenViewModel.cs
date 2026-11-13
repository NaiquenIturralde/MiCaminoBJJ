using MiCaminoBJJ.Web.Services.Recorrido;

namespace MiCaminoBJJ.Web.ViewModels.Recorrido;

/// <summary>Resumen de ficha técnica para listados (módulo e índice).</summary>
public sealed class FichaResumenViewModel
{
    public string Slug { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string TipoContenido { get; init; } = string.Empty;

    /// <summary>Cinturón principal de la técnica relacionada, o "Sin asignar".</summary>
    public string CinturonTexto { get; init; } = string.Empty;

    /// <summary>true cuando el cinturón principal quedó sin asignar a propósito.</summary>
    public bool CinturonSinAsignar { get; init; }

    /// <summary>true cuando el cinturón principal NO es blanco (material complementario).</summary>
    public bool MaterialComplementario { get; init; }

    public bool TieneVideo { get; init; }
    public bool RequiereRevisionInstructor { get; init; }

    public string Ruta => $"/recorrido/cinturon-blanco/ficha/{Slug}";

    public static FichaResumenViewModel Desde(FichaBlanco ficha) => new()
    {
        Slug = ficha.Slug,
        Nombre = ficha.Nombre,
        TipoContenido = ficha.TipoContenido,
        CinturonTexto = ficha.CinturonPrincipal ?? "Sin asignar",
        CinturonSinAsignar = ficha.CinturonPrincipal is null,
        MaterialComplementario = ficha.CinturonPrincipal is not null
                                 && !string.Equals(ficha.CinturonPrincipal, "Blanco", StringComparison.OrdinalIgnoreCase),
        TieneVideo = ficha.Video is not null,
        RequiereRevisionInstructor = ficha.RequiereRevisionInstructor
    };
}
