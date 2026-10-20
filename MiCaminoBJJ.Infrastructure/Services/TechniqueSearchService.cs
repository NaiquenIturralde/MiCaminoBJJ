using Microsoft.EntityFrameworkCore;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

public class TechniqueSearchService : ITechniqueSearchService
{
    private readonly AppDbContext _context;

    public TechniqueSearchService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Technique>> SearchAsync(TechniqueSearchFilter filter)
    {
        var query = _context.Techniques
            .Include(t => t.Belt)
            .Include(t => t.Category)
            .Include(t => t.Position)
            .Where(t => t.IsActive)
            .AsQueryable();

        // Search by term (case-insensitive)
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.ToLowerInvariant();
            query = query.Where(t =>
                t.Name.ToLower().Contains(term) ||
                t.Description.ToLower().Contains(term) ||
                (t.Tips != null && t.Tips.ToLower().Contains(term)));
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
