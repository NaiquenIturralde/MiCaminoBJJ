using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Web.ViewModels;

/// <summary>
/// Opción de un desplegable de filtro (cinturón, categoría o posición).
/// Para los cinturones además incluye la imagen PNG transparente y la clase
/// CSS de glow que alimentan las tarjetas visuales de filtro de la biblioteca.
/// </summary>
public sealed class FiltroOpcionViewModel
{
    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Ruta virtual de la imagen del cinturón (solo opciones de cinturón).</summary>
    public string? Imagen { get; init; }

    /// <summary>Clase CSS que aplica el glow del color del cinturón (solo opciones de cinturón).</summary>
    public string? ClaseGlow { get; init; }

    public static FiltroOpcionViewModel DesdeBelt(Belt belt)
    {
        var visual = CinturonVisual.Para(belt.Name);
        return new FiltroOpcionViewModel
        {
            Id = belt.Id,
            Nombre = belt.Name,
            Imagen = visual?.Imagen,
            ClaseGlow = visual?.Clase
        };
    }

    public static FiltroOpcionViewModel DesdeCategoria(TechniqueCategory category) =>
        new() { Id = category.Id, Nombre = category.Name };

    public static FiltroOpcionViewModel DesdePosicion(Position position) =>
        new() { Id = position.Id, Nombre = position.Name };
}
