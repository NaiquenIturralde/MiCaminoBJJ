using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;

namespace MiCaminoBJJ.Web.Services;

/// <summary>
/// Consulta de solo lectura de la biblioteca de técnicas para la capa Web (Etapa 1).
///
/// Contrato de reemplazo: está pensada para que una implementación futura sobre
/// SQL Server sustituya a la actual SIN tocar controladores ni vistas
/// (mismos tipos de Core: <see cref="Technique"/>, <see cref="Belt"/>,
/// <see cref="TechniqueCategory"/>, <see cref="Position"/>, <see cref="TechniqueAlias"/>).
///
/// IMPLEMENTACIÓN ACTUAL (Etapa 2B): <see cref="TecnicasSqlServerConsultaService"/>,
/// que consulta SQL Server (base exclusiva MiCaminoBJJ_Web_TFI_2026) con
/// AsNoTracking y sin fallback a datos en memoria. La implementación de
/// demostración en memoria (<see cref="TecnicasDemoConsultaService"/>) se conserva
/// como fuente de datos del seeder y para las pruebas, pero ya no está registrada
/// en DI. La propiedad <see cref="EsFuenteDeDemostracion"/> permite etiquetar la
/// procedencia de los datos en la interfaz, de modo que nunca se mezclen datos
/// reales y demostrativos sin identificarlos.
/// </summary>
public interface ITecnicasConsultaService
{
    /// <summary>True cuando la fuente es una implementación de demostración en memoria.</summary>
    bool EsFuenteDeDemostracion { get; }

    /// <summary>
    /// Técnicas activas que cumplen el filtro (nombre/alias + cinturón + categoría + posición).
    /// Misma semántica de búsqueda normalizada que <c>ITechniqueSearchService</c>
    /// (regla de <c>TextNormalizer</c>: sin tildes, mayúsculas, ñ↔n).
    /// </summary>
    Task<IReadOnlyList<Technique>> SearchAsync(TechniqueSearchFilter filter);

    /// <summary>Técnica por identificador; null si no existe.</summary>
    Task<Technique?> GetByIdAsync(int id);

    /// <summary>
    /// Técnica cuyo nombre O ALIAS coincide EXACTAMENTE (tras normalizar) con
    /// el término, INCLUYENDO técnicas ocultas (IsActive = 0); null si no hay.
    ///
    /// Uso exclusivo del recorrido de cinturón blanco: sus fichas referencian
    /// técnicas por nombre histórico y la importación del PDF renombra 4 de
    /// ellas (el nombre anterior queda como alias) además de ocultar las 7
    /// sin video de la biblioteca pública. <see cref="SearchAsync"/> no sirve
    /// para eso: exige coincidencia por "contains" en nombre y SOLO devuelve
    /// técnicas activas.
    /// </summary>
    Task<Technique?> GetByNombreOAliasExactoAsync(string? nombre);

    /// <summary>Cinturones disponibles en la fuente (para los filtros).</summary>
    Task<IReadOnlyList<Belt>> GetBeltsAsync();

    /// <summary>Categorías disponibles en la fuente (para los filtros).</summary>
    Task<IReadOnlyList<TechniqueCategory>> GetCategoriesAsync();

    /// <summary>Posiciones disponibles en la fuente (para los filtros).</summary>
    Task<IReadOnlyList<Position>> GetPositionsAsync();
}
