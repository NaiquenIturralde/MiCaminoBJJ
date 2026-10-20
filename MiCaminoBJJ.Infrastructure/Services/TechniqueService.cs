using Microsoft.EntityFrameworkCore;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

public class TechniqueService : ITechniqueService
{
    private readonly AppDbContext _context;

    public TechniqueService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Technique?> GetByIdAsync(int techniqueId)
    {
        return await _context.Techniques
            .Include(t => t.Belt)
            .Include(t => t.Category)
            .Include(t => t.Position)
            .Include(t => t.OfficialMedia).ThenInclude(tm => tm.MediaAsset)
            .FirstOrDefaultAsync(t => t.Id == techniqueId && t.IsActive);
    }

    public async Task<List<Technique>> GetAllAsync()
    {
        return await _context.Techniques
            .Include(t => t.Belt)
            .Include(t => t.Category)
            .Include(t => t.Position)
            .Where(t => t.IsActive)
            .OrderBy(t => t.Belt.DisplayOrder)
            .ThenBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<List<Technique>> GetAllForAdminAsync()
    {
        return await _context.Techniques
            .Include(t => t.Belt)
            .Include(t => t.Category)
            .Include(t => t.Position)
            .OrderBy(t => t.IsActive)
            .ThenBy(t => t.Belt.DisplayOrder)
            .ThenBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<List<Technique>> GetByBeltIdAsync(int beltId)
    {
        return await _context.Techniques
            .Include(t => t.Belt)
            .Include(t => t.Category)
            .Include(t => t.Position)
            .Where(t => t.BeltId == beltId && t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<List<Technique>> GetByCategoryIdAsync(int categoryId)
    {
        return await _context.Techniques
            .Include(t => t.Belt)
            .Include(t => t.Category)
            .Include(t => t.Position)
            .Where(t => t.CategoryId == categoryId && t.IsActive)
            .ToListAsync();
    }

    public async Task<List<Technique>> GetByPositionIdAsync(int positionId)
    {
        return await _context.Techniques
            .Include(t => t.Belt)
            .Include(t => t.Category)
            .Include(t => t.Position)
            .Where(t => t.PositionId == positionId && t.IsActive)
            .ToListAsync();
    }

    public async Task<Technique?> GetLastViewedByUserAsync(int userId)
    {
        var lastView = await _context.TechniqueViews
            .Where(tv => tv.UserId == userId)
            .OrderByDescending(tv => tv.ViewedAt)
            .FirstOrDefaultAsync();

        if (lastView == null)
            return null;

        return await GetByIdAsync(lastView.TechniqueId);
    }

    public async Task<Technique> CreateAsync(string name, string description, string objective, string? steps, string? tips, int beltId, int categoryId, int positionId)
    {
        var technique = new Technique
        {
            Name = name,
            Description = description,
            Objective = objective,
            Steps = steps,
            Tips = tips,
            BeltId = beltId,
            CategoryId = categoryId,
            PositionId = positionId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Techniques.Add(technique);
        await _context.SaveChangesAsync();
        return technique;
    }

    public async Task UpdateAsync(int techniqueId, string name, string description, string objective, string? steps, string? tips, int beltId, int categoryId, int positionId)
    {
        var technique = await _context.Techniques.FindAsync(techniqueId);
        if (technique == null)
            return;

        technique.Name = name;
        technique.Description = description;
        technique.Objective = objective;
        technique.Steps = steps;
        technique.Tips = tips;
        technique.BeltId = beltId;
        technique.CategoryId = categoryId;
        technique.PositionId = positionId;
        technique.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task SetActiveAsync(int techniqueId, bool isActive)
    {
        var technique = await _context.Techniques.FindAsync(techniqueId);
        if (technique == null)
            return;

        technique.IsActive = isActive;
        technique.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }
}
