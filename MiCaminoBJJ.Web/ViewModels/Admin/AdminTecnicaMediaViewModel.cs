namespace MiCaminoBJJ.Web.ViewModels.Admin;

/// <summary>
/// Sección «Multimedia de la técnica» del formulario administrativo de Editar.
///
/// Vive FUERA del &lt;form&gt; principal (HTML no permite formularios anidados):
/// cada recurso y el alta tienen su propio formulario, que publica en las
/// acciones administrativas dedicadas de <c>AdminTecnicasMediaController</c>
/// (todas con autorización Administrador + antiforgery).
///
/// Conserva los tres filtros del listado para que, tras agregar/editar/quitar
/// un recurso, el redirect vuelva a Editar con los mismos criterios y «Cancelar»
/// siga funcionando.
/// </summary>
public sealed class AdminTecnicaMediaViewModel
{
    /// <summary>Técnica a la que pertenece la sección.</summary>
    public int TecnicaId { get; init; }

    /// <summary>Recursos asociados (vacío = estado vacío claro en la vista).</summary>
    public IReadOnlyList<AdminMediaRecursoViewModel> Recursos { get; init; } = Array.Empty<AdminMediaRecursoViewModel>();

    /// <summary>Formulario de alta de enlace de video.</summary>
    public AdminMediaAltaViewModel Alta { get; init; } = new();

    /// <summary>True si la técnica todavía no tiene ningún recurso asociado.</summary>
    public bool Vacio => Recursos.Count == 0;

    /// <summary>True si algún recurso puede mostrar reproductor integrado (YouTube/Vimeo).</summary>
    public bool TieneVideoIntegrado => Recursos.Any(r => r.EsVideoIntegrado);

    // ── Conservación de los filtros del listado (idéntico al form principal) ──

    public string? FiltroBuscar { get; init; }
    public bool? FiltroEstado { get; init; }
    public int? FiltroCinturon { get; init; }

    /// <summary>Valor textual del estado para el campo oculto ("", "true" o "false").</summary>
    public string FiltroEstadoTexto => FiltroEstado switch
    {
        true => "true",
        false => "false",
        null => string.Empty
    };

    /// <summary>Query string de los filtros (para enlaces de vuelta a Editar).</summary>
    public string QueryStringFiltro =>
        AdminTecnicasFiltros.QueryString(FiltroBuscar, FiltroEstado, FiltroCinturon);
}
