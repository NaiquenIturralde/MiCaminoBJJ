using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Web.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MiCaminoBJJ.Web.Services;

/// <summary>
/// Carga inicial controlada e idempotente de la biblioteca web (Etapa 2B).
///
/// ¿Qué carga? El MISMO catálogo de la demostración en memoria (11 técnicas,
/// 5 cinturones, 5 categorías, 5 posiciones y sus alias), reutilizando
/// <see cref="TecnicasDemoConsultaService"/> como fuente de datos para no
/// duplicar el catálogo en el código. Se persisten entidades frescas (solo
/// claves foráneas, sin reutilizar los Ids en memoria de la demo) para que
/// SQL Server asigne las identidades.
///
/// Idempotencia: TODO el alta se resuelve con "merge por nombre normalizado".
/// Si una fila ya existe (por nombre de cinturón/categoría/posición/técnica,
/// o por alias dentro de su técnica) simplemente no se inserta; una segunda
/// ejecución no agrega filas ni duplica datos. Si el catálogo base ya existe
/// pero faltan técnicas (corte por la mitad en una corrida anterior), se
/// completan usando los Ids existentes. Además, una técnica cuyo nombre ya
/// existe como ALIAS de otra (renombrada por la importación del PDF) se
/// considera existente y NUNCA se re-inserta: evita duplicar la biblioteca.
///
/// Alcance deliberado: NO reutiliza los seeders de SQLite (pueden operar
/// usuarios, progreso u otras entidades ajenas al alcance web), NO crea
/// usuarios administrativos, contraseñas ni datos personales, y NO ejecuta
/// migraciones: la base debe estar migrada antes (Fase 2).
/// </summary>
public static class WebLibrarySeeder
{
    /// <summary>Resultado de la carga, útil para log y pruebas.</summary>
    public sealed record WebLibrarySeedResult(
        int TecnicasAgregadas,
        int TecnicasExistentes,
        int AliasesAgregados,
        int CinturonesAgregados,
        int CategoriasAgregadas,
        int PosicionesAgregadas);

    /// <summary>
    /// Ejecuta la carga inicial. Idempotente: repetirla no duplica datos.
    /// Lanza si la base no está disponible o si las tablas están en un estado
    /// inconsistente (no degrada silenciosamente).
    /// </summary>
    public static async Task<WebLibrarySeedResult> SeedAsync(
        WebTfDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);

        // Origen de datos: el catálogo de demostración YA existente en el proyecto.
        var fuente = new TecnicasDemoConsultaService();
        var cinturones = await fuente.GetBeltsAsync();
        var categorias = await fuente.GetCategoriesAsync();
        var posiciones = await fuente.GetPositionsAsync();
        var tecnicas = await fuente.SearchAsync(new TechniqueSearchFilter());

        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);

        // ── Catálogo base: merge por nombre normalizado ──
        var beltsBd = await db.Belts.AsNoTracking().ToListAsync(cancellationToken);
        var idsCinturones = await MergeCatalogo(
            db,
            db.Belts,
            beltsBd,
            cinturones.Select(b => (Nombre: b.Name, DisplayOrder: b.DisplayOrder)),
            b => new Belt { Name = b.Nombre, DisplayOrder = b.DisplayOrder },
            b => b.Id,
            b => TextNormalizer.Normalize(b.Nombre),
            b => TextNormalizer.Normalize(b.Name),
            cancellationToken);
        int cinturonesAgregados = idsCinturones.Nuevos;

        var categoriasBd = await db.TechniqueCategories.AsNoTracking().ToListAsync(cancellationToken);
        var idsCategorias = await MergeCatalogo(
            db,
            db.TechniqueCategories,
            categoriasBd,
            categorias.Select(c => (Nombre: c.Name, DisplayOrder: c.DisplayOrder)),
            c => new TechniqueCategory { Name = c.Nombre, DisplayOrder = c.DisplayOrder },
            c => c.Id,
            c => TextNormalizer.Normalize(c.Nombre),
            c => TextNormalizer.Normalize(c.Name),
            cancellationToken);
        int categoriasAgregadas = idsCategorias.Nuevos;

        var posicionesBd = await db.Positions.AsNoTracking().ToListAsync(cancellationToken);
        var idsPosiciones = await MergeCatalogo(
            db,
            db.Positions,
            posicionesBd,
            posiciones.Select(p => (Nombre: p.Name, DisplayOrder: p.DisplayOrder)),
            p => new Position { Name = p.Nombre, DisplayOrder = p.DisplayOrder },
            p => p.Id,
            p => TextNormalizer.Normalize(p.Nombre),
            p => TextNormalizer.Normalize(p.Name),
            cancellationToken);
        int posicionesAgregadas = idsPosiciones.Nuevos;

        // ── Técnicas: merge por nombre normalizado + alias por técnica ──
        var tecnicasBd = await db.Techniques
            .AsNoTracking()
            .Include(t => t.Aliases)
            .ToListAsync(cancellationToken);

        var porNombre = new Dictionary<string, Technique>(StringComparer.Ordinal);
        foreach (var existente in tecnicasBd)
            porNombre[TextNormalizer.Normalize(existente.NameNormalized ?? existente.Name)] = existente;

        int tecnicasAgregadas = 0;
        int tecnicasExistentes = 0;
        int aliasesAgregados = 0;

        foreach (var origen in tecnicas)
        {
            var clave = TextNormalizer.Normalize(origen.Name);

            if (porNombre.TryGetValue(clave, out var existente))
            {
                tecnicasExistentes++;
                aliasesAgregados += CompletarAliases(db, existente, origen);
                continue;
            }

            // Técnica RENOMBRADA en la base real (la importación del PDF deja el
            // nombre histórico como ALIAS de otra técnica): NO se re-inserta.
            // Sin esta guarda, el merge por nombre reintroduciría la técnica
            // vieja y duplicaría la biblioteca pública cada vez que arranca.
            var yaEsAlias = await db.TechniqueAliases.AsNoTracking()
                .AnyAsync(a => a.NameNormalized == clave, cancellationToken);
            if (yaEsAlias)
            {
                tecnicasExistentes++;
                continue;
            }

            var nueva = new Technique
            {
                Name = origen.Name,
                NameNormalized = TextNormalizer.Normalize(origen.Name),
                Description = origen.Description,
                Objective = origen.Objective,
                Steps = origen.Steps,
                Tips = origen.Tips,
                IsActive = origen.IsActive,
                CreatedAt = origen.CreatedAt,
                BeltId = idsCinturones.Ids[TextNormalizer.Normalize(origen.Belt.Name)],
                CategoryId = idsCategorias.Ids[TextNormalizer.Normalize(origen.Category.Name)],
                PositionId = idsPosiciones.Ids[TextNormalizer.Normalize(origen.Position.Name)],
                Aliases = origen.Aliases
                    .Select(a => new TechniqueAlias
                    {
                        Name = a.Name,
                        NameNormalized = a.NameNormalized,
                        CreatedAt = a.CreatedAt
                    })
                    .ToList()
            };

            db.Techniques.Add(nueva);
            porNombre[clave] = nueva;
            tecnicasAgregadas++;
            aliasesAgregados += nueva.Aliases.Count;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);

        var resultado = new WebLibrarySeedResult(
            tecnicasAgregadas,
            tecnicasExistentes,
            aliasesAgregados,
            cinturonesAgregados,
            categoriasAgregadas,
            posicionesAgregadas);

        if (resultado.TecnicasAgregadas == 0 && resultado.TecnicasExistentes > 0)
        {
            logger.LogInformation(
                "Biblioteca web: catálogo ya inicializado ({Total} técnicas). Nada que cargar.",
                resultado.TecnicasExistentes);
        }
        else
        {
            logger.LogInformation(
                "Biblioteca web: carga inicial completada. +{Tecnicas} técnicas, +{Aliases} alias, "
                + "+{Cinturones} cinturones, +{Categorias} categorías, +{Posiciones} posiciones.",
                resultado.TecnicasAgregadas,
                resultado.AliasesAgregados,
                resultado.CinturonesAgregados,
                resultado.CategoriasAgregadas,
                resultado.PosicionesAgregadas);
        }

        return resultado;
    }

    /// <summary>
    /// Merge genérico del catálogo base (cinturones/categorías/posiciones):
    /// agrega SOLO las filas cuyo nombre normalizado aún no existe y devuelve
    /// el mapa nombreNormalizado→Id resultante junto con la cantidad agregada.
    /// </summary>
    private static async Task<(Dictionary<string, int> Ids, int Nuevos)> MergeCatalogo<T>(
        WebTfDbContext db,
        Microsoft.EntityFrameworkCore.DbSet<T> set,
        IEnumerable<T> existentes,
        IEnumerable<(string Nombre, int DisplayOrder)> catalogo,
        Func<(string Nombre, int DisplayOrder), T> fabrica,
        Func<T, int> obtenerId,
        Func<(string Nombre, int DisplayOrder), string> claveOrigen,
        Func<T, string> claveExistente,
        CancellationToken cancellationToken)
        where T : class
    {
        var mapa = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var fila in existentes)
        {
            var clave = claveExistente(fila);
            if (!mapa.ContainsKey(clave))
                mapa[clave] = obtenerId(fila);
        }

        int agregados = 0;
        foreach (var item in catalogo)
        {
            var clave = claveOrigen(item);
            if (mapa.ContainsKey(clave))
                continue;

            set.Add(fabrica(item));
            mapa[clave] = 0; // Id provisorio hasta persistir.
            agregados++;
        }

        if (agregados > 0)
        {
            await db.SaveChangesAsync(cancellationToken);

            // Releer los Ids reales recién asignados por SQL Server.
            var filasActualizadas = await set.AsNoTracking().ToListAsync(cancellationToken);
            mapa.Clear();
            foreach (var fila in filasActualizadas)
            {
                var clave = claveExistente(fila);
                if (!mapa.ContainsKey(clave))
                    mapa[clave] = obtenerId(fila);
            }
        }

        return (mapa, agregados);
    }

    /// <summary>Agrega a la técnica los alias del catálogo que aún no existen.</summary>
    private static int CompletarAliases(
        WebTfDbContext db,
        Technique existente,
        Technique origen)
    {
        var actuales = existente.Aliases
            .Select(a => TextNormalizer.Normalize(a.Name))
            .ToHashSet(StringComparer.Ordinal);

        int agregados = 0;
        foreach (var aliasOrigen in origen.Aliases)
        {
            if (actuales.Contains(aliasOrigen.NameNormalized))
                continue;

            db.TechniqueAliases.Add(new TechniqueAlias
            {
                TechniqueId = existente.Id,
                Name = aliasOrigen.Name,
                NameNormalized = aliasOrigen.NameNormalized,
                CreatedAt = aliasOrigen.CreatedAt
            });
            agregados++;
        }

        return agregados;
    }
}
