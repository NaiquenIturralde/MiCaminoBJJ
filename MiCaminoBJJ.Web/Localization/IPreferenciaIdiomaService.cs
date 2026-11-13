namespace MiCaminoBJJ.Web.Localization;

/// <summary>
/// Persistencia de la preferencia de idioma del practicante.
///
/// Utiliza la tabla <c>UserSettings</c> del dominio (columna
/// <c>Language</c>, ya existente en el esquema: NO requiere migración) con
/// una relación 1:1 por <c>UserId</c>.
/// </summary>
public interface IPreferenciaIdiomaService
{
    /// <summary>
    /// Idioma guardado del practicante (código normalizado: "es", "en" o
    /// "pt"), o null si nunca eligió uno.
    /// </summary>
    Task<string?> ObtenerAsync(int perfilId, CancellationToken cancelacion = default);

    /// <summary>
    /// Guarda (crea o actualiza) el idioma del practicante. Lanza
    /// <see cref="ArgumentException"/> si el código no está soportado.
    /// </summary>
    Task GuardarAsync(int perfilId, string idioma, CancellationToken cancelacion = default);
}
