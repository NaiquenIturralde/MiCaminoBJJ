namespace MiCaminoBJJ.Core.Entities;

public class TechniqueCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Technique> Techniques { get; set; } = new List<Technique>();
}
