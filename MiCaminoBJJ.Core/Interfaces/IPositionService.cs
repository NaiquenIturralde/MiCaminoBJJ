using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Core.Interfaces;

public interface IPositionService
{
    Task<List<Position>> GetAllAsync();
    Task<Position?> GetByIdAsync(int positionId);
}
