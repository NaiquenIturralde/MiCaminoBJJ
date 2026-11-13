namespace MiCaminoBJJ.Web.ViewModels;

/// <summary>
/// Estado del listado de la biblioteca: filtro aplicado, resultados y opciones
/// de los desplegables. Se reconstruye en cada GET para que los controles
/// conserven los valores buscados.
/// </summary>
public sealed class TecnicasIndexViewModel
{
    public TecnicasFiltroViewModel Filtro { get; set; } = new();

    public IReadOnlyList<TecnicaCardViewModel> Tecnicas { get; set; } = Array.Empty<TecnicaCardViewModel>();

    public IReadOnlyList<FiltroOpcionViewModel> Cinturones { get; set; } = Array.Empty<FiltroOpcionViewModel>();

    public IReadOnlyList<FiltroOpcionViewModel> Categorias { get; set; } = Array.Empty<FiltroOpcionViewModel>();

    public IReadOnlyList<FiltroOpcionViewModel> Posiciones { get; set; } = Array.Empty<FiltroOpcionViewModel>();

    /// <summary>Etiqueta de procedencia de los datos (demostración vs. real).</summary>
    public bool EsFuenteDeDemostracion { get; set; }

    /// <summary>Mensaje claro para el usuario cuando la consulta falla.</summary>
    public string? MensajeError { get; set; }

    public int TotalResultados => Tecnicas.Count;

    public bool HayResultados => Tecnicas.Count > 0;

    /// <summary>Ruta al detalle de una técnica conservando los filtros aplicados.</summary>
    public string RutaDetalle(int id) => Filtro.ToRutaDetalle(id);
}
