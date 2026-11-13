using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Core.Interfaces;

/// <summary>
/// Gestión de sinónimos de técnicas. Punto único de escritura de alias:
/// normaliza el nombre con TextNormalizer y garantiza que no haya
/// duplicados (normalizados) dentro de la misma técnica.
/// </summary>
public interface IAliasService
{
    Task<List<TechniqueAlias>> GetByTechniqueAsync(int techniqueId);
    Task<TechniqueAlias> AddAsync(int techniqueId, string name);
    Task<bool> RemoveAsync(int aliasId);
}
