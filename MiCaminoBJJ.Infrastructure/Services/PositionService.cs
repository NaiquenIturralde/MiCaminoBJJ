using Microsoft.EntityFrameworkCore;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

public class PositionService : IPositionService
{
    private readonly AppDbContext _context;

    public PositionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Position>> GetAllAsync()
    {
        return await _context.Positions
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();
    }

    public async Task<Position?> GetByIdAsync(int positionId)
    {
        return await _context.Positions
            .FirstOrDefaultAsync(p => p.Id == positionId);
    }
}
