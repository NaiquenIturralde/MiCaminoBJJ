using Microsoft.EntityFrameworkCore;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _context;

    public CategoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<TechniqueCategory>> GetAllAsync()
    {
        return await _context.TechniqueCategories
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();
    }

    public async Task<TechniqueCategory?> GetByIdAsync(int categoryId)
    {
        return await _context.TechniqueCategories
            .FirstOrDefaultAsync(c => c.Id == categoryId);
    }
}
