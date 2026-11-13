using MiCaminoBJJ.Core.Interfaces;

namespace MiCaminoBJJ.Web.ViewModels;

/// <summary>
/// Filtros de la biblioteca, reflejados 1:1 en los parámetros GET del listado
/// (/Tecnicas?buscar=..&cinturon=..&categoria=..&posicion=..).
/// Conserva los valores aplicados para re-renderizar el formulario y para
/// reconstruir las rutas de detalle/vuelta con los mismos criterios.
/// </summary>
public sealed class TecnicasFiltroViewModel
{
    public const string ParamBuscar = "buscar";
    public const string ParamCinturon = "cinturon";
    public const string ParamCategoria = "categoria";
    public const string ParamPosicion = "posicion";

    public string? Buscar { get; set; }
    public int? Cinturon { get; set; }
    public int? Categoria { get; set; }
    public int? Posicion { get; set; }

    public bool TieneFiltros =>
        !string.IsNullOrWhiteSpace(Buscar) || Cinturon.HasValue || Categoria.HasValue || Posicion.HasValue;

    /// <summary>Query string de los filtros aplicados ("?a=1&amp;b=2") o vacío si no hay filtros.</summary>
    public string ToQueryString() => ConstruirQueryString(Cinturon);

    /// <summary>
    /// Ruta del listado filtrando por el cinturón indicado (null → "Todos"),
    /// conservando el resto de los filtros aplicados (búsqueda, categoría y
    /// posición). Así las tarjetas visuales de cinturón de la biblioteca
    /// alternan sin perder lo que el practicante ya había elegido.
    /// </summary>
    public string RutaConCinturon(int? id) => "/Tecnicas" + ConstruirQueryString(id);

    private string ConstruirQueryString(int? cinturon)
    {
        var partes = new List<string>(4);

        if (!string.IsNullOrWhiteSpace(Buscar))
            partes.Add($"{ParamBuscar}={Uri.EscapeDataString(Buscar.Trim())}");
        if (cinturon.HasValue)
            partes.Add($"{ParamCinturon}={cinturon.Value}");
        if (Categoria.HasValue)
            partes.Add($"{ParamCategoria}={Categoria.Value}");
        if (Posicion.HasValue)
            partes.Add($"{ParamPosicion}={Posicion.Value}");

        return partes.Count == 0 ? string.Empty : "?" + string.Join("&", partes);
    }

    /// <summary>Ruta del listado conservando los filtros (para "volver" desde el detalle).</summary>
    public string ToRutaListado() => "/Tecnicas" + ToQueryString();

    /// <summary>Ruta del detalle conservando los filtros, para poder volver sin perderlos.</summary>
    public string ToRutaDetalle(int id) => $"/Tecnicas/Details/{id}" + ToQueryString();

    /// <summary>Traducción a los filtros del dominio (mismo tipo que usa ITechniqueSearchService).</summary>
    public TechniqueSearchFilter ToSearchFilter() => new()
    {
        SearchTerm = Buscar,
        BeltId = Cinturon,
        CategoryId = Categoria,
        PositionId = Posicion
    };
}
