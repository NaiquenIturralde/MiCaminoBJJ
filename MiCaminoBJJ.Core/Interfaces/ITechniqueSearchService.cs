using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Core.Interfaces;

public class TechniqueSearchFilter
{
    public string? SearchTerm { get; set; }
    public int? BeltId { get; set; }
    public int? CategoryId { get; set; }
    public int? PositionId { get; set; }
}

public interface ITechniqueSearchService
{
    Task<List<Technique>> SearchAsync(TechniqueSearchFilter filter);
}
