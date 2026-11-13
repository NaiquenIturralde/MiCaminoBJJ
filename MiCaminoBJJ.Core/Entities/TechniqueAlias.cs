namespace MiCaminoBJJ.Core.Entities;

/// <summary>
/// Nombre alternativo (sinónimo) de una técnica.
/// Permite encontrar la misma técnica por variantes de nombre sin duplicar filas en Techniques.
/// </summary>
/// <remarks>
/// Compatibilidad futura con localización (es/en/pt): si se necesita traducir alias,
/// se agrega una columna <c>Locale</c> (string, ej. "es") e índice compuesto
/// (TechniqueId, Locale, NameNormalized) en una migración aditiva posterior.
/// El diseño actual (un nombre por fila) no la bloquea.
/// </remarks>
public class TechniqueAlias
{
    public int Id { get; set; }
    public int TechniqueId { get; set; }
    /// <summary>Nombre tal como lo escribió el usuario (legible).</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Versión normalizada con TextNormalizer.Normalize (sin tildes, mayúsculas).</summary>
    public string NameNormalized { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Technique Technique { get; set; } = null!;
}
