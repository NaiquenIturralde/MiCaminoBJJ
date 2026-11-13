using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Web.ViewModels.Recorrido;

/// <summary>
/// Técnica EXISTENTE de la biblioteca mostrada en el recorrido del cinturón
/// blanco. Se resuelve en runtime desde <c>ITecnicasConsultaService</c>: no se
/// duplica la entidad ni se le cambia el cinturón principal.
/// </summary>
public sealed class TecnicaRelacionadaViewModel
{
    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public string Cinturon { get; init; } = string.Empty;
    public string Categoria { get; init; } = string.Empty;
    public string Posicion { get; init; } = string.Empty;

    public string Ruta => $"/Tecnicas/Details/{Id}";

    public static TecnicaRelacionadaViewModel Desde(Technique technique) => new()
    {
        Id = technique.Id,
        Nombre = technique.Name,
        Descripcion = technique.Description,
        Cinturon = technique.Belt?.Name ?? string.Empty,
        Categoria = technique.Category?.Name ?? string.Empty,
        Posicion = technique.Position?.Name ?? string.Empty
    };
}
