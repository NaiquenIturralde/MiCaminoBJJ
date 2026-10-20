using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Core.Interfaces;

public interface ICategoryService
{
    Task<List<TechniqueCategory>> GetAllAsync();
    Task<TechniqueCategory?> GetByIdAsync(int categoryId);
}
