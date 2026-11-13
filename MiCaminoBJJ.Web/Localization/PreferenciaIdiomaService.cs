using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Web.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MiCaminoBJJ.Web.Localization;

/// <summary>
/// Implementación de <see cref="IPreferenciaIdiomaService"/> sobre
/// <c>UserSettings.Language</c>.
///
/// Reutiliza la tabla del dominio que YA existe en el esquema SQL Server
/// (migración <c>InitialSqlServer</c>): no se crean tablas nuevas, no se
/// agregan columnas y no hace falta ejecutar migraciones.
///
/// La fila se crea solo la primera vez (upsert): el índice ÚNICO sobre
/// <c>UserId</c> garantiza que un practicante nunca tenga dos preferencias.
/// </summary>
public sealed class PreferenciaIdiomaService : IPreferenciaIdiomaService
{
    private readonly WebTfDbContext _db;

    public PreferenciaIdiomaService(WebTfDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <inheritdoc />
    public async Task<string?> ObtenerAsync(int perfilId, CancellationToken cancelacion = default)
    {
        if (perfilId <= 0)
            return null;

        var idioma = await _db.UserSettings
            .AsNoTracking()
            .Where(a => a.UserId == perfilId)
            .Select(a => a.Language)
            .FirstOrDefaultAsync(cancelacion);

        return Idiomas.Normalizar(idioma);
    }

    /// <inheritdoc />
    public async Task GuardarAsync(int perfilId, string idioma, CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idioma);

        if (perfilId <= 0)
            throw new ArgumentOutOfRangeException(nameof(perfilId), "El perfil debe ser un identificador válido.");

        var codigo = Idiomas.Normalizar(idioma)
            ?? throw new ArgumentException($"El idioma '{idioma}' no está soportado.", nameof(idioma));

        var ajustes = await _db.UserSettings
            .FirstOrDefaultAsync(a => a.UserId == perfilId, cancelacion);

        if (ajustes is null)
        {
            ajustes = new UserSettings { UserId = perfilId };
            _db.UserSettings.Add(ajustes);
        }

        ajustes.Language = codigo;

        await _db.SaveChangesAsync(cancelacion);
    }
}
