using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Web.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MiCaminoBJJ.Web.Services;

/// <summary>
/// Implementación de <see cref="ITecnicasConsultaService"/> sobre SQL Server
/// (Etapa 2B): consulta de SOLO LECTURA contra <see cref="WebTfDbContext"/>.
///
/// Decisiones:
/// - <c>AsNoTracking()</c> en todas las consultas: la web nunca modifica datos.
/// - Sin fallback a datos en memoria: si la base no responde, la excepción
///   propaga y el controlador registra el error y muestra el mensaje de
///   "No se pudo cargar" (nunca datos de demostración presentados como reales).
/// - Misma semántica de búsqueda que la demo: término normalizado sobre
///   <c>NameNormalized</c> + alias normalizados + descripción/consejos literal
///   (insensible a mayúsculas por el collation de la base).
/// - <see cref="EsFuenteDeDemostracion"/> = false: la insignia "Datos de
///   demostración" desaparece de las vistas automáticamente.
/// </summary>
public sealed class TecnicasSqlServerConsultaService : ITecnicasConsultaService
{
    private readonly WebTfDbContext _db;

    public TecnicasSqlServerConsultaService(WebTfDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>La fuente es SQL Server real: nunca es demostración.</summary>
    public bool EsFuenteDeDemostracion => false;

    /// <summary>
    /// Técnicas activas que cumplen el filtro (búsqueda por nombre/alias +
    /// cinturón + categoría + posición), consultadas con AsNoTracking.
    /// </summary>
    public async Task<IReadOnlyList<Technique>> SearchAsync(TechniqueSearchFilter filter)
    {
        filter ??= new TechniqueSearchFilter();

        IQueryable<Technique> query = _db.Techniques
            .AsNoTracking()
            .Include(t => t.Belt)
            .Include(t => t.Category)
            .Include(t => t.Position)
            .Include(t => t.Aliases)
            .Where(t => t.IsActive);

        var searchTerm = filter.SearchTerm;
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalized = TextNormalizer.Normalize(searchTerm);
            if (normalized.Length > 0)
            {
                var literal = searchTerm.Trim();
                query = query.Where(t =>
                    (t.NameNormalized != null && t.NameNormalized.Contains(normalized)) ||
                    t.Aliases.Any(a => a.NameNormalized.Contains(normalized)) ||
                    t.Description.Contains(literal) ||
                    (t.Tips != null && t.Tips.Contains(literal)));
            }
        }

        if (filter.BeltId.HasValue)
            query = query.Where(t => t.BeltId == filter.BeltId.Value);

        if (filter.CategoryId.HasValue)
            query = query.Where(t => t.CategoryId == filter.CategoryId.Value);

        if (filter.PositionId.HasValue)
            query = query.Where(t => t.PositionId == filter.PositionId.Value);

        return await query
            .OrderBy(t => t.Belt.DisplayOrder)
            .ThenBy(t => t.Name)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<Technique?> GetByNombreOAliasExactoAsync(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return null;

        var normalizado = TextNormalizer.Normalize(nombre);
        if (normalizado.Length == 0)
            return null;

        // Pre-selección por "contains" sobre nombre O alias — SIN filtrar
        // IsActive: el recorrido puede referenciar técnicas ocultadas por la
        // importación del PDF (las 7 administrativas sin video).
        var candidatas = await _db.Techniques
            .AsNoTracking()
            .Include(t => t.Belt)
            .Include(t => t.Aliases)
            .Where(t =>
                (t.NameNormalized != null && t.NameNormalized.Contains(normalizado)) ||
                t.Aliases.Any(a => a.NameNormalized != null && a.NameNormalized.Contains(normalizado)))
            .ToListAsync();

        // Match EXACTO en memoria con TextNormalizer sobre nombre O alias:
        // la misma regla del resolvedor del recorrido, ahora tolerante a los
        // renombrados de la importación (nombre histórico = alias).
        return candidatas.FirstOrDefault(t =>
            TextNormalizer.Normalize(t.NameNormalized ?? t.Name) == normalizado ||
            t.Aliases.Any(a => TextNormalizer.Normalize(a.NameNormalized ?? a.Name) == normalizado));
    }

    /// <summary>
    /// Técnica por identificador con sus relaciones; null si no existe.
    /// NO filtra IsActive (misma regla que la demo: el controlador decide el 404).
    /// Multimedia: OfficialMedia + MediaAsset viajan incluidos para que la card
    /// "Multimedia" pueda construir el reproductor (mismo patrón
    /// Include/ThenInclude que Infrastructure/Services/TechniqueService.cs).
    /// </summary>
    public async Task<Technique?> GetByIdAsync(int id)
    {
        return await _db.Techniques
            .AsNoTracking()
            .Include(t => t.Belt)
            .Include(t => t.Category)
            .Include(t => t.Position)
            .Include(t => t.Aliases)
            .Include(t => t.OfficialMedia).ThenInclude(tm => tm.MediaAsset)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    /// <summary>Cinturones disponibles para los filtros.</summary>
    public async Task<IReadOnlyList<Belt>> GetBeltsAsync()
    {
        return await _db.Belts
            .AsNoTracking()
            .OrderBy(b => b.DisplayOrder)
            .ToListAsync();
    }

    /// <summary>Categorías disponibles para los filtros.</summary>
    public async Task<IReadOnlyList<TechniqueCategory>> GetCategoriesAsync()
    {
        return await _db.TechniqueCategories
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();
    }

    /// <summary>Posiciones disponibles para los filtros.</summary>
    public async Task<IReadOnlyList<Position>> GetPositionsAsync()
    {
        return await _db.Positions
            .AsNoTracking()
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();
    }
}
