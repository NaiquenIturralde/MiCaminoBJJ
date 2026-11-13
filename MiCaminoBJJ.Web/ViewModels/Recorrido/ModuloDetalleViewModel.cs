using MiCaminoBJJ.Web.Services.Recorrido;

namespace MiCaminoBJJ.Web.ViewModels.Recorrido;

/// <summary>Punto teórico de un módulo (título + texto) para la vista.</summary>
public sealed class PuntoContenidoViewModel
{
    public string Titulo { get; init; } = string.Empty;
    public string Texto { get; init; } = string.Empty;

    public static PuntoContenidoViewModel Desde(PuntoContenido punto) => new()
    {
        Titulo = punto.Titulo,
        Texto = punto.Texto
    };
}

/// <summary>Vista de detalle de un módulo educativo del recorrido.</summary>
public sealed class ModuloDetalleViewModel
{
    public int Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Titulo { get; init; } = string.Empty;
    public string Resumen { get; init; } = string.Empty;

    public IReadOnlyList<PuntoContenidoViewModel> Puntos { get; init; } = Array.Empty<PuntoContenidoViewModel>();

    /// <summary>Fichas técnicas propias del módulo (solo el módulo 5 tiene).</summary>
    public IReadOnlyList<FichaResumenViewModel> Fichas { get; init; } = Array.Empty<FichaResumenViewModel>();

    /// <summary>Fichas de otros módulos enlazadas como material relacionado.</summary>
    public IReadOnlyList<FichaResumenViewModel> FichasRelacionadas { get; init; } = Array.Empty<FichaResumenViewModel>();

    public bool TieneVideo { get; init; }
    public string EstadoVideoTexto { get; init; } = string.Empty;

    /// <summary>Aviso discreto de video pendiente (módulos sin demostración confirmada).</summary>
    public string NotaVideoPendiente { get; init; } = CinturonBlancoCatalogo.NotaVideoPendiente;

    /// <summary>true = mostrar el recuadro que distingue SUPERVIVENCIA de ESCAPE.</summary>
    public bool MuestraDiferenciaSupervivenciaEscape { get; init; }

    public string TextoDiferenciaSupervivenciaEscape { get; init; } =
        CinturonBlancoCatalogo.NotaSupervivenciaVsEscape;

    public string NotaMaterialComplementario { get; init; } = CinturonBlancoCatalogo.NotaMaterialComplementario;

    public string RutaIndice => "/recorrido/cinturon-blanco";

    public bool TieneAnterior { get; init; }
    public string? SlugAnterior { get; init; }
    public string? TituloAnterior { get; init; }

    public bool TieneSiguiente { get; init; }
    public string? SlugSiguiente { get; init; }
    public string? TituloSiguiente { get; init; }

    public string RutaAnterior => $"/recorrido/cinturon-blanco/modulo/{SlugAnterior}";
    public string RutaSiguiente => $"/recorrido/cinturon-blanco/modulo/{SlugSiguiente}";

    public static ModuloDetalleViewModel Desde(
        ModuloBlanco modulo,
        ModuloBlanco? anterior,
        ModuloBlanco? siguiente,
        IReadOnlyList<FichaResumenViewModel>? fichasRelacionadas = null) => new()
        {
            Id = modulo.Id,
            Slug = modulo.Slug,
            Titulo = modulo.Titulo,
            Resumen = modulo.Resumen,
            Puntos = modulo.Puntos.Select(PuntoContenidoViewModel.Desde).ToList(),
            Fichas = modulo.Fichas.Select(FichaResumenViewModel.Desde).ToList(),
            FichasRelacionadas = fichasRelacionadas ?? Array.Empty<FichaResumenViewModel>(),
            TieneVideo = modulo.EstadoVideo == EstadoVideoModulo.ConVideo,
            EstadoVideoTexto = modulo.EstadoVideo == EstadoVideoModulo.ConVideo
            ? "Con video"
            : "Video pendiente",
            MuestraDiferenciaSupervivenciaEscape = modulo.Id is 3 or 4,
            TieneAnterior = anterior is not null,
            SlugAnterior = anterior?.Slug,
            TituloAnterior = anterior?.Titulo,
            TieneSiguiente = siguiente is not null,
            SlugSiguiente = siguiente?.Slug,
            TituloSiguiente = siguiente?.Titulo
        };
}
