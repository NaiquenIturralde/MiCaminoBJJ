using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace MiCaminoBJJ.Web.Services.Admin;

/// <summary>
/// Implementación SQL Server de las consultas administrativas (Etapa 3):
/// <see cref="WebTfDbContext"/> con AsNoTracking, misma semántica de búsqueda
/// normalizada que la biblioteca pública pero SIN filtrar IsActive
/// (el listado administrativo muestra el estado completo).
/// </summary>
public sealed class AdminTecnicasQueryService : IAdminTecnicasQueryService
{
    private readonly WebTfDbContext _db;

    public AdminTecnicasQueryService(WebTfDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<Technique>> ListarAsync(string? buscar, bool? activa, int? cinturon)
    {
        IQueryable<Technique> query = _db.Techniques
            .AsNoTracking()
            .Include(t => t.Belt)
            .Include(t => t.Category)
            .Include(t => t.Position);

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var normalized = TextNormalizer.Normalize(buscar);
            if (normalized.Length > 0)
            {
                var literal = buscar.Trim();
                query = query.Where(t =>
                    (t.NameNormalized != null && t.NameNormalized.Contains(normalized)) ||
                    t.Aliases.Any(a => a.NameNormalized.Contains(normalized)) ||
                    t.Description.Contains(literal));
            }
        }

        if (activa.HasValue)
            query = query.Where(t => t.IsActive == activa.Value);

        // Filtro por cinturón: se resuelve en SQL (WHERE BeltId = @p) sobre el
        // catálogo existente; no altera el resto de los criterios y sigue
        // incluyendo técnicas desactivadas cuando el estado no lo restringe.
        if (cinturon.HasValue)
            query = query.Where(t => t.BeltId == cinturon.Value);

        return await query
            .OrderBy(t => t.Belt.DisplayOrder)
            .ThenBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<Technique?> ObtenerAsync(int id)
    {
        return await _db.Techniques
            .AsNoTracking()
            .Include(t => t.Belt)
            .Include(t => t.Category)
            .Include(t => t.Position)
            .Include(t => t.Aliases)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IReadOnlyList<Belt>> ObtenerCinturonesAsync() =>
        await _db.Belts
            .AsNoTracking()
            .OrderBy(b => b.DisplayOrder)
            .ToListAsync();

    public async Task<AdminOpcionesViewModel> ObtenerOpcionesAsync()
    {
        var cinturones = await _db.Belts.AsNoTracking().OrderBy(b => b.DisplayOrder).ToListAsync();
        var categorias = await _db.TechniqueCategories.AsNoTracking().OrderBy(c => c.DisplayOrder).ToListAsync();
        var posiciones = await _db.Positions.AsNoTracking().OrderBy(p => p.DisplayOrder).ToListAsync();

        return new AdminOpcionesViewModel(
            cinturones.Select(FiltroOpcionViewModel.DesdeBelt).ToList(),
            categorias.Select(FiltroOpcionViewModel.DesdeCategoria).ToList(),
            posiciones.Select(FiltroOpcionViewModel.DesdePosicion).ToList());
    }
}
