using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Core.Interfaces;

public interface IBeltService
{
    Task<List<Belt>> GetAllAsync();
    Task<Belt?> GetByIdAsync(int beltId);
}
