using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

/// <summary>
/// Backfill idempotente de <c>Technique.NameNormalized</c> usando EXACTAMENTE
/// TextNormalizer.Normalize (C#), nunca UPPER()/COLLATE de SQLite.
/// Corre después de MigrateAsync en cada inicio; solo toca filas sin normalizar.
/// </summary>
public class DataNormalizationService : IDataNormalizationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DataNormalizationService> _logger;

    public DataNormalizationService(AppDbContext context, ILogger<DataNormalizationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> BackfillTechniqueNamesAsync()
    {
        // Idempotente: solo filas aún sin normalizar (null o vacío).
        var pending = await _context.Techniques
            .Where(t => t.NameNormalized == null || t.NameNormalized == "")
            .ToListAsync();

        if (pending.Count == 0)
            return 0;

        foreach (var technique in pending)
            technique.NameNormalized = TextNormalizer.Normalize(technique.Name);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Backfill normalizado: {Count} técnicas actualizadas", pending.Count);

        return pending.Count;
    }
}
