using MiCaminoBJJ.Web.Services.Recorrido;

namespace MiCaminoBJJ.Web.ViewModels.Recorrido;

/// <summary>Tarjeta de módulo educativo para el índice del recorrido.</summary>
public sealed class ModuloResumenViewModel
{
    public int Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Titulo { get; init; } = string.Empty;
    public string Resumen { get; init; } = string.Empty;
    public int TotalPuntos { get; init; }
    public int TotalFichas { get; init; }

    /// <summary>true = el módulo tiene al menos una ficha con video confirmado.</summary>
    public bool TieneVideo { get; init; }

    /// <summary>Texto del indicador de estado del video ("Video pendiente" cuando no hay demostración).</summary>
    public string EstadoVideoTexto { get; init; } = string.Empty;

    public string Ruta => $"/recorrido/cinturon-blanco/modulo/{Slug}";

    public static ModuloResumenViewModel Desde(ModuloBlanco modulo) => new()
    {
        Id = modulo.Id,
        Slug = modulo.Slug,
        Titulo = modulo.Titulo,
        Resumen = modulo.Resumen,
        TotalPuntos = modulo.Puntos.Count,
        TotalFichas = modulo.Fichas.Count,
        TieneVideo = modulo.EstadoVideo == EstadoVideoModulo.ConVideo,
        EstadoVideoTexto = modulo.EstadoVideo == EstadoVideoModulo.ConVideo
            ? "Con video"
            : "Video pendiente"
    };
}
