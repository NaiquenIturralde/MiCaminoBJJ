using Microsoft.EntityFrameworkCore;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

public class SavedTechniqueService : ISavedTechniqueService
{
    private readonly AppDbContext _context;

    public SavedTechniqueService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<SavedTechnique>> GetByUserAsync(int userId)
    {
        return await _context.SavedTechniques
            .Include(st => st.Technique)
                .ThenInclude(t => t.Belt)
            .Include(st => st.Technique)
                .ThenInclude(t => t.Category)
            .Include(st => st.Technique)
                .ThenInclude(t => t.Position)
            .Where(st => st.UserId == userId)
            .OrderByDescending(st => st.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> IsSavedAsync(int userId, int techniqueId)
    {
        return await _context.SavedTechniques
            .AnyAsync(st => st.UserId == userId && st.TechniqueId == techniqueId);
    }

    public async Task<SavedTechnique?> GetSavedAsync(int userId, int techniqueId)
    {
        return await _context.SavedTechniques
            .FirstOrDefaultAsync(st => st.UserId == userId && st.TechniqueId == techniqueId);
    }

    public async Task<SavedTechnique> SaveAsync(int userId, int techniqueId, SavedType savedType)
    {
        var existing = await _context.SavedTechniques
            .FirstOrDefaultAsync(st => st.UserId == userId && st.TechniqueId == techniqueId);

        if (existing != null)
        {
            existing.SavedType = savedType;
            await _context.SaveChangesAsync();
            return existing;
        }

        var saved = new SavedTechnique
        {
            UserId = userId,
            TechniqueId = techniqueId,
            SavedType = savedType
        };

        _context.SavedTechniques.Add(saved);
        await _context.SaveChangesAsync();
        return saved;
    }

    public async Task ChangeTypeAsync(int savedId, SavedType newType)
    {
        var saved = await _context.SavedTechniques.FindAsync(savedId);
        if (saved != null)
        {
            saved.SavedType = newType;
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveAsync(int savedId)
    {
        var saved = await _context.SavedTechniques.FindAsync(savedId);
        if (saved != null)
        {
            _context.SavedTechniques.Remove(saved);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> GetFavoriteCountAsync(int userId)
    {
        return await _context.SavedTechniques
            .CountAsync(st => st.UserId == userId && st.SavedType == SavedType.Favorite);
    }

    public async Task<int> GetWatchLaterCountAsync(int userId)
    {
        return await _context.SavedTechniques
            .CountAsync(st => st.UserId == userId && st.SavedType == SavedType.WatchLater);
    }
}
