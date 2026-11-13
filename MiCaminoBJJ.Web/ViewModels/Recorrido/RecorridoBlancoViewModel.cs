using MiCaminoBJJ.Web.Services.Recorrido;

namespace MiCaminoBJJ.Web.ViewModels.Recorrido;

/// <summary>
/// Índice del recorrido Cinturón Blanco (Etapa 6A): encabezado educativo,
/// tarjetas de los cinco módulos y listado de técnicas existentes adecuadas
/// para principiantes (resueltas contra la biblioteca real).
/// </summary>
public sealed class RecorridoBlancoViewModel
{
    public string Titulo { get; init; } = CinturonBlancoCatalogo.Titulo;
    public string Subtitulo { get; init; } = CinturonBlancoCatalogo.Subtitulo;
    public string Descripcion { get; init; } = CinturonBlancoCatalogo.Descripcion;
    public string Introduccion { get; init; } = CinturonBlancoCatalogo.Introduccion;
    public string MarcoPedagogico { get; init; } = CinturonBlancoCatalogo.MarcoPedagogico;
    public string NotaMaterialComplementario { get; init; } = CinturonBlancoCatalogo.NotaMaterialComplementario;

    public IReadOnlyList<ModuloResumenViewModel> Modulos { get; init; } = Array.Empty<ModuloResumenViewModel>();

    /// <summary>Todas las fichas técnicas del recorrido (módulo 5).</summary>
    public IReadOnlyList<FichaResumenViewModel> Fichas { get; init; } = Array.Empty<FichaResumenViewModel>();

    /// <summary>Técnicas de cinturón blanco existentes en la biblioteca.</summary>
    public IReadOnlyList<TecnicaRelacionadaViewModel> Tecnicas { get; set; } = Array.Empty<TecnicaRelacionadaViewModel>();

    /// <summary>Mensaje discreto cuando la biblioteca no pudo responder.</summary>
    public string? MensajeTecnicas { get; set; }

    public bool HayTecnicas => Tecnicas.Count > 0;

    /// <summary>Insignia "Datos de demostración" si la fuente de la biblioteca lo indica.</summary>
    public bool EsFuenteDeDemostracion { get; set; }

    public static RecorridoBlancoViewModel DesdeCatalogo() => new()
    {
        Modulos = CinturonBlancoCatalogo.Modulos.Select(ModuloResumenViewModel.Desde).ToList(),
        Fichas = CinturonBlancoCatalogo.Fichas.Select(FichaResumenViewModel.Desde).ToList()
    };
}
