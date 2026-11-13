using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Web.ViewModels;

namespace MiCaminoBJJ.Web.Services.Admin;

/// <summary>
/// Consultas de solo lectura del módulo administrativo (Etapa 3).
/// Separadas de los comandos: todo es AsNoTracking y no introduce escrituras.
/// A diferencia de la biblioteca pública, el listado INCLUYE técnicas
/// desactivadas (el administrador debe verlas para reactivarlas).
/// </summary>
public interface IAdminTecnicasQueryService
{
    /// <summary>
    /// Todas las técnicas (activas y desactivadas) con búsqueda opcional por
    /// nombre/alias normalizado y filtro opcional por cinturón; orden por
    /// cinturón y nombre. Los tres criterios se combinan entre sí.
    /// </summary>
    /// <param name="buscar">Nombre/sinónimo a buscar; null o vacío = sin búsqueda.</param>
    /// <param name="activa">true = solo activas, false = solo desactivadas, null = ambas.</param>
    /// <param name="cinturon">Id del cinturón del catálogo; null = todos los cinturones.</param>
    Task<IReadOnlyList<Technique>> ListarAsync(string? buscar, bool? activa, int? cinturon);

    /// <summary>Técnica por Id (incluida si está desactivada); null si no existe.</summary>
    Task<Technique?> ObtenerAsync(int id);

    /// <summary>
    /// Catálogo completo de cinturones (Blanco, Azul, Violeta, Marrón, Negro)
    /// ordenado por <see cref="Core.Entities.Belt.DisplayOrder"/>: es la fuente
    /// de las opciones del filtro por cinturón del listado administrativo.
    /// </summary>
    Task<IReadOnlyList<Belt>> ObtenerCinturonesAsync();

    /// <summary>Opciones para los desplegables del formulario.</summary>
    Task<AdminOpcionesViewModel> ObtenerOpcionesAsync();
}

/// <summary>Opciones de formulario (cinturones, categorías y posiciones).</summary>
public sealed record AdminOpcionesViewModel(
    IReadOnlyList<FiltroOpcionViewModel> Cinturones,
    IReadOnlyList<FiltroOpcionViewModel> Categorias,
    IReadOnlyList<FiltroOpcionViewModel> Posiciones);
