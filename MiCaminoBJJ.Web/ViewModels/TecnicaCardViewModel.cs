using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Web.ViewModels;

/// <summary>
/// Tarjeta de técnica para el listado. Derivada SOLO de propiedades reales de
/// <see cref="Technique"/> (y sus entidades relacionadas); no inventa campos.
/// </summary>
public sealed class TecnicaCardViewModel
{
    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public string Cinturon { get; init; } = string.Empty;
    public string Categoria { get; init; } = string.Empty;
    public string Posicion { get; init; } = string.Empty;

    public static TecnicaCardViewModel Desde(Technique technique) => new()
    {
        Id = technique.Id,
        Nombre = technique.Name,
        Descripcion = technique.Description,
        Cinturon = technique.Belt?.Name ?? string.Empty,
        Categoria = technique.Category?.Name ?? string.Empty,
        Posicion = technique.Position?.Name ?? string.Empty
    };
}
