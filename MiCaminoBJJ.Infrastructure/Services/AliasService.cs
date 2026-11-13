using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

public class AliasService : IAliasService
{
    // Límite existente: TechniqueAliasConfiguration.Name/NameNormalized HasMaxLength(200).
    private const int MaxAliasLength = 200;

    // Códigos SQLite de interés:
    //   19  = SQLITE_CONSTRAINT (código primario de cualquier violación de constraint)
    //   2067 = SQLITE_CONSTRAINT_UNIQUE
    //   1555 = SQLITE_CONSTRAINT_PRIMARYKEY
    private const int SqliteConstraint = 19;
    private const int SqliteConstraintUnique = 2067;
    private const int SqliteConstraintPrimaryKey = 1555;

    private readonly AppDbContext _context;
    private readonly ILogger<AliasService> _logger;

    public AliasService(AppDbContext context, ILogger<AliasService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<TechniqueAlias>> GetByTechniqueAsync(int techniqueId)
    {
        return await _context.TechniqueAliases
            .Where(a => a.TechniqueId == techniqueId)
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<TechniqueAlias> AddAsync(int techniqueId, string name)
    {
        var techniqueExists = await _context.Techniques.AnyAsync(t => t.Id == techniqueId);
        if (!techniqueExists)
            throw new KeyNotFoundException($"La técnica {techniqueId} no existe.");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El alias no puede estar vacío.", nameof(name));

        var readableName = name.Trim();
        var normalized = TextNormalizer.Normalize(readableName);

        // Nombre sin caracteres significativos (p. ej. solo marcas diacríticas):
        // normaliza a vacío y colisionaría con cualquier otro alias equivalente.
        if (normalized.Length == 0)
            throw new ArgumentException("El alias debe contener caracteres significativos.", nameof(name));

        // Se mantiene el límite existente (HasMaxLength(200) de la configuración EF).
        // SQLite no valida longitud en columnas TEXT: se valida en el servicio.
        if (readableName.Length > MaxAliasLength)
            throw new ArgumentException($"El alias no puede superar los {MaxAliasLength} caracteres.", nameof(name));

        if (normalized.Length > MaxAliasLength)
            throw new ArgumentException($"El alias normalizado no puede superar los {MaxAliasLength} caracteres.", nameof(name));

        // Anti-duplicados por BD (índice único) + validación con mensaje claro.
        var duplicate = await _context.TechniqueAliases
            .AnyAsync(a => a.TechniqueId == techniqueId && a.NameNormalized == normalized);

        if (duplicate)
            throw new InvalidOperationException(
                $"Ya existe un alias equivalente para esta técnica: \"{name}\".");

        var alias = new TechniqueAlias
        {
            TechniqueId = techniqueId,
            Name = readableName,
            NameNormalized = normalized
        };

        _context.TechniqueAliases.Add(alias);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Único caso traducido a "alias duplicado": violación real del índice
            // único (validación previa + índice como última línea ante carreras).
            // Errores FK, bloqueo, disco o conexión NO entran aquí y se propagan tal cual.
            _context.Entry(alias).State = EntityState.Detached;
            throw new InvalidOperationException(
                $"Ya existe un alias equivalente para esta técnica: \"{name}\".", ex);
        }

        _logger.LogInformation("Alias {AliasId} '{Alias}' added to technique {TechniqueId}",
            alias.Id, alias.Name, techniqueId);

        return alias;
    }

    public async Task<bool> RemoveAsync(int aliasId)
    {
        var alias = await _context.TechniqueAliases.FindAsync(aliasId);
        if (alias == null)
            return false;

        _context.TechniqueAliases.Remove(alias);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Alias {AliasId} removed from technique {TechniqueId}",
            aliasId, alias.TechniqueId);

        return true;
    }

    /// <summary>
    /// Identifica SOLO violaciones de unicidad (SQLITE_CONSTRAINT_UNIQUE / PRIMARYKEY).
    /// Distingue por código de error: FK (787), busy/locked, IO o conexión no se
    /// confunden con duplicados y se re-lanzan sin traducir.
    /// </summary>
    internal static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is not Microsoft.Data.Sqlite.SqliteException sqlEx)
            return false;

        // Algunas builds exponen directamente el código extendido.
        if (sqlEx.SqliteErrorCode is SqliteConstraintUnique or SqliteConstraintPrimaryKey)
            return true;

        // Código primario 19 (constraint genérico): el extendido discrimina el tipo.
        return sqlEx.SqliteErrorCode == SqliteConstraint &&
               sqlEx.SqliteExtendedErrorCode is SqliteConstraintUnique or SqliteConstraintPrimaryKey;
    }
}
