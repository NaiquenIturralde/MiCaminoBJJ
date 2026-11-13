using Microsoft.EntityFrameworkCore;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

public class TechniqueSearchService : ITechniqueSearchService
{
    private readonly AppDbContext _context;

    public TechniqueSearchService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Búsqueda de técnicas con dos estrategias documentadas:
    /// <list type="bullet">
    /// <item><description><b>Nombre y alias (sinónimos):</b> comparación NORMALIZADA contra
    /// <c>NameNormalized</c>/<c>Alias.NameNormalized</c> con la regla de <c>TextNormalizer</c>
    /// (sin tildes, ñ↔n, mayúsculas). Tolera variantes de escritura.</description></item>
    /// <item><description><b>Description y Tips:</b> comparación LITERAL case-insensitive ASCII
    /// (ToLower de SQLite), como antes de la etapa 2.2. "estrangulacion" (sin tilde) NO
    /// encontrará "Estrangulación" en descripción pero SÍ en nombre. Diferencia consciente:
    /// normalizar descripciones requeriría nuevas columnas/migración (fuera de alcance).</description></item>
    /// </list>
    /// Término vacío o normalizable a vacío → sin filtro de texto. Todo se traduce a SQL
    /// parametrizado; el catálogo nunca se carga en memoria. Filtros por cinturón,
    /// categoría y posición se componen con AND.
    /// </summary>
    public async Task<List<Technique>> SearchAsync(TechniqueSearchFilter filter)
    {
        var query = _context.Techniques
            .Include(t => t.Belt)
            .Include(t => t.Category)
            .Include(t => t.Position)
            .Where(t => t.IsActive)
            .AsQueryable();

        // Search by term: normalización en C# (una sola regla: TextNormalizer) y
        // comparación SQL contra columnas pre-normalizadas. EF traduce a
        // LIKE parametrizado + EXISTS sobre alias; el catálogo NUNCA se carga en memoria.
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var normalizedTerm = TextNormalizer.Normalize(filter.SearchTerm);
            var literalTerm = filter.SearchTerm.Trim();

            if (normalizedTerm.Length > 0)
            {
                // Nombre y alias: match normalizado (tolera tildes/mayúsculas/ñ).
                // Descripción/Tips: match literal previo conservado (sin columna normalizada).
                query = query.Where(t =>
                    (t.NameNormalized != null && t.NameNormalized.Contains(normalizedTerm)) ||
                    t.Aliases.Any(a => a.NameNormalized.Contains(normalizedTerm)) ||
                    t.Description.ToLower().Contains(literalTerm.ToLower()) ||
                    (t.Tips != null && t.Tips.ToLower().Contains(literalTerm.ToLower())));
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
}
