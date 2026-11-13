namespace MiCaminoBJJ.Core.Interfaces;

/// <summary>
/// Backfill idempotente de textos normalizados en la base existente.
/// Se ejecuta al iniciar la aplicación, después de aplicar migraciones.
/// Única operación segura que garantiza que filas previas a la normalización
/// (creadas por seed o por la app antes de esta etapa) queden consultables
/// con la MISMA regla de TextNormalizer (nunca con UPPER() de SQLite).
/// </summary>
public interface IDataNormalizationService
{
    /// <summary>
    /// Normaliza los nombres de técnicas que aún no tienen valor normalizado.
    /// Idempotente: solo toca filas con NameNormalized null/vacío; repetir no altera datos.
    /// Devuelve la cantidad de filas actualizadas.
    /// </summary>
    Task<int> BackfillTechniqueNamesAsync();
}
