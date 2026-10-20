using Microsoft.EntityFrameworkCore;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

public class BeltService : IBeltService
{
    private readonly AppDbContext _context;

    public BeltService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Belt>> GetAllAsync()
    {
        return await _context.Belts
            .OrderBy(b => b.DisplayOrder)
            .ToListAsync();
    }

    public async Task<Belt?> GetByIdAsync(int beltId)
    {
        return await _context.Belts
            .FirstOrDefaultAsync(b => b.Id == beltId);
    }
}
