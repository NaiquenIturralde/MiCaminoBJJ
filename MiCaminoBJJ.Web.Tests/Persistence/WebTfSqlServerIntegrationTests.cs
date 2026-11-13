using System.Text.Json;
using FluentAssertions;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Services;
using MiCaminoBJJ.Web.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MiCaminoBJJ.Web.Tests.Persistence;

/// <summary>
/// Pruebas de INTEGRACIÓN de la persistencia SQL Server de la web (Etapa 2B).
///
/// Corren contra una base de PRUEBA AISLADA y ÚNICA POR EJECUCIÓN
/// (prefijo <c>MiCaminoBJJ_Web_TFI_2026_Test</c> + identificador de corrida)
/// creada por <see cref="CatalogoAisladoFixture"/>: jamás contra la principal.
/// La base se crea desde las migraciones del proyecto —previo control de que
/// no existiera— y se elimina al final validando la conexión efectiva.
///
/// Si LocalDB no está disponible, TODAS las pruebas fallan con un mensaje claro
/// (sin salteos silenciosos ni alternativas de servidor).
/// </summary>
[Collection("SqlServerLocalDb")]
public sealed class WebTfSqlServerIntegrationTests
{
    private readonly CatalogoAisladoFixture _fixture;

    public WebTfSqlServerIntegrationTests(CatalogoAisladoFixture fixture) => _fixture = fixture;

    private void VerificarDisponibilidad() => _fixture.VerificarDisponibilidad();

    // ── Conexión y base exclusiva ──────────────────────────────────────────

    [Fact]
    public async Task Conexion_ApuntaALaBaseAislada_Y_EstaMigrada()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();

        // Fase 2: la prueba deja de exigir la base principal; valida que el
        // destino sea la base AISLADA del fixture.
        Assert.Equal(_fixture.DatabaseName, db.Database.GetDbConnection().Database);
        TestDatabaseGuard.EsBasePrincipal(db.Database.GetDbConnection().Database).Should().BeFalse();

        var pendientes = await db.Database.GetPendingMigrationsAsync();
        Assert.Empty(pendientes);

        var historial = await ConsultarStringsAsync(db,
            "SELECT MigrationId FROM [__EFMigrationsHistory]");
        Assert.Contains("20261030150827_InitialSqlServer", historial);
    }

    [Fact]
    public async Task Migracion_ConservaTodasLasTablasDeDominio_Incluido_AuditLog()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();

        var tablas = (await ConsultarStringsAsync(db,
            "SELECT name FROM sys.tables WHERE name <> '__EFMigrationsHistory'"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Invariante válido ANTES y DESPUÉS de aplicar AddIdentity (Etapa 4B):
        // las 20 tablas de dominio deben estar siempre presentes; Identity solo
        // suma tablas nuevas sin tocar estas.
        var dominio = new[]
        {
            "Achievements", "AuditLogs", "Belts", "MediaAssets", "Positions",
            "SavedTechniques", "TechniqueAliases", "TechniqueCategories", "TechniqueMedia",
            "TechniquePractices", "Techniques", "TechniqueViews", "TrainingSessions",
            "UserAchievements", "UserProfileMedia", "Users", "UserSettings",
            "UserTechniqueProgress", "UserTechniqueResources", "UserTournaments"
        };
        Assert.True(tablas.IsSupersetOf(dominio), "las 20 tablas de dominio no pueden desaparecer");
        Assert.True(tablas.Count >= 20, "mínimo las 20 tablas de dominio (Identity suma nuevas al aplicarse)");

        Assert.Contains("AuditLogs", tablas);
        Assert.Contains("Techniques", tablas);
        Assert.Contains("TechniqueAliases", tablas);
        Assert.Contains("TrainingSessions", tablas);

        var fks = await ConsultarStringsAsync(db,
            "SELECT COUNT(*) FROM sys.foreign_keys");
        Assert.True(int.Parse(fks.Single()) >= 26, "Se esperaban al menos 26 claves foráneas.");
    }

    // ── Carga inicial y ausencia de duplicados ─────────────────────────────

    [Fact]
    public async Task Seed_Carga_El_Catalogo_Completo_Con_Relaciones_Validas()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();

        Assert.Equal(11, await db.Techniques.CountAsync());
        Assert.Equal(5, await db.Belts.CountAsync());
        Assert.Equal(5, await db.TechniqueCategories.CountAsync());
        Assert.Equal(5, await db.Positions.CountAsync());
        Assert.Equal(13, await db.TechniqueAliases.CountAsync());

        var cinturones = (await db.Belts.AsNoTracking().Select(b => b.Id).ToListAsync()).ToHashSet();
        var categorias = (await db.TechniqueCategories.AsNoTracking().Select(c => c.Id).ToListAsync()).ToHashSet();
        var posiciones = (await db.Positions.AsNoTracking().Select(p => p.Id).ToListAsync()).ToHashSet();
        var tecnicasIds = (await db.Techniques.AsNoTracking().Select(t => t.Id).ToListAsync()).ToHashSet();

        var tecnicas = await db.Techniques.AsNoTracking().ToListAsync();

        // Integridad referencial de los datos cargados.
        Assert.All(tecnicas, t =>
        {
            Assert.Contains(t.BeltId, cinturones);
            Assert.Contains(t.CategoryId, categorias);
            Assert.Contains(t.PositionId, posiciones);
            Assert.False(string.IsNullOrWhiteSpace(t.NameNormalized));
        });

        var aliases = await db.TechniqueAliases.AsNoTracking().ToListAsync();
        Assert.All(aliases, a => Assert.Contains(a.TechniqueId, tecnicasIds));

        // Nombres y alias sin duplicados (normalizados).
        Assert.Equal(tecnicas.Count, tecnicas.Select(t => t.NameNormalized).Distinct().Count());
        Assert.Equal(aliases.Count, aliases
            .GroupBy(a => (a.TechniqueId, a.NameNormalized))
            .Count());
    }

    [Fact]
    public async Task Seed_Es_Idempotente_No_Duplica_Datos()
    {
        VerificarDisponibilidad();

        int tecnicasAntes, aliasesAntes, cinturonesAntes;
        await using (var db = _fixture.CreateContext())
        {
            tecnicasAntes = await db.Techniques.CountAsync();
            aliasesAntes = await db.TechniqueAliases.CountAsync();
            cinturonesAntes = await db.Belts.CountAsync();
        }

        await using (var db = _fixture.CreateContext())
        {
            var segunda = await WebLibrarySeeder.SeedAsync(db, NullLogger.Instance);

            Assert.Equal(0, segunda.TecnicasAgregadas);
            Assert.Equal(0, segunda.AliasesAgregados);
            Assert.Equal(0, segunda.CinturonesAgregados);
            Assert.Equal(0, segunda.CategoriasAgregadas);
            Assert.Equal(0, segunda.PosicionesAgregadas);
            Assert.Equal(11, segunda.TecnicasExistentes);
        }

        await using (var db = _fixture.CreateContext())
        {
            Assert.Equal(tecnicasAntes, await db.Techniques.CountAsync());
            Assert.Equal(aliasesAntes, await db.TechniqueAliases.CountAsync());
            Assert.Equal(cinturonesAntes, await db.Belts.CountAsync());
        }
    }

    // ── Servicio de consulta real (Fase 4) ─────────────────────────────────

    [Fact]
    public async Task Servicio_No_Es_Demostracion()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        var servicio = new TecnicasSqlServerConsultaService(db);

        Assert.False(servicio.EsFuenteDeDemostracion);
    }

    [Fact]
    public async Task Busqueda_Por_Nombre_Y_Alias_Contra_SqlServer()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        var servicio = new TecnicasSqlServerConsultaService(db);

        // Por alias: "RNC" encuentra Mata león (sin que el nombre lo contenga).
        var porAlias = await servicio.SearchAsync(new TechniqueSearchFilter { SearchTerm = "RNC" });
        Assert.Single(porAlias);
        Assert.Equal("Mata león", porAlias[0].Name);

        // Normalización: sin tildes y mayúsculas en el término.
        var sinTilde = await servicio.SearchAsync(new TechniqueSearchFilter { SearchTerm = "escapE dE mOntadA" });
        Assert.Contains(sinTilde, t => t.Name == "Escape de montada");

        // Búsqueda que NO debe encontrar nada.
        var vacia = await servicio.SearchAsync(new TechniqueSearchFilter { SearchTerm = "technique inexistent xyz" });
        Assert.Empty(vacia);

        // Cinturones/categorías/posiciones para los filtros.
        var cinturones = await servicio.GetBeltsAsync();
        Assert.Equal(5, cinturones.Count);
        Assert.Equal(new[] { "Blanco", "Azul", "Violeta", "Marrón", "Negro" },
            cinturones.Select(c => c.Name).ToArray());
    }

    [Fact]
    public async Task Filtros_Combinados_Contra_SqlServer()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        var servicio = new TecnicasSqlServerConsultaService(db);

        var azul = (await servicio.GetBeltsAsync()).Single(b => b.Name == "Azul");
        var sumision = (await servicio.GetCategoriesAsync()).Single(c => c.Name == "Sumisión");
        var guardiaCerrada = (await servicio.GetPositionsAsync()).Single(p => p.Name == "Guardia cerrada");

        var resultado = await servicio.SearchAsync(new TechniqueSearchFilter
        {
            BeltId = azul.Id,
            CategoryId = sumision.Id,
            PositionId = guardiaCerrada.Id
        });

        Assert.Equal(new[] { "Kimura desde guardia cerrada", "Triángulo desde guardia cerrada" },
            resultado.Select(t => t.Name).OrderBy(n => n, StringComparer.CurrentCulture).ToArray());

        // Búsqueda + filtros a la vez: "kimura" dentro del cinturón Azul
        // encuentra Kimura desde guardia cerrada Y Defensa de kimura (ambas Azul),
        // pero NO técnicas de otros cinturones.
        var combinado = await servicio.SearchAsync(new TechniqueSearchFilter
        {
            SearchTerm = "kimura",
            BeltId = azul.Id
        });
        Assert.Contains(combinado, t => t.Name == "Kimura desde guardia cerrada");
        Assert.Contains(combinado, t => t.Name == "Defensa de kimura en control lateral");
        Assert.DoesNotContain(combinado, t => t.Name == "Escape de montada"); // cinturón Blanco
    }

    [Fact]
    public async Task Detalle_Por_Id_Con_Relaciones_Y_Alias()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        var servicio = new TecnicasSqlServerConsultaService(db);

        // Los Ids los asigna SQL Server (identity): se resuelve por nombre para
        // no depender de valores de demostración en memoria.
        var idMataLeon = await db.Techniques.AsNoTracking()
            .Where(t => t.Name == "Mata león")
            .Select(t => t.Id)
            .SingleAsync();

        var mataLeon = await servicio.GetByIdAsync(idMataLeon);
        Assert.NotNull(mataLeon);
        Assert.Equal("Mata león", mataLeon!.Name);
        Assert.NotNull(mataLeon.Belt);
        Assert.NotNull(mataLeon.Category);
        Assert.NotNull(mataLeon.Position);
        Assert.Contains(mataLeon.Aliases, a => a.Name == "RNC");
        Assert.False(string.IsNullOrWhiteSpace(mataLeon.Steps));

        // Id inexistente → null (el controlador responde HTTP 404).
        Assert.Null(await servicio.GetByIdAsync(999999));
    }

    // ── Integridad referencial y configuración ─────────────────────────────

    [Fact]
    public async Task IntegridadReferencial_Rechaza_Tecnica_Con_Cinturon_Inexistente()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();

        db.Techniques.Add(new Technique
        {
            Name = "Técnica inválida para prueba",
            NameNormalized = "TECNICA INVALIDA PARA PRUEBA",
            Description = "No debe persistirse.",
            Objective = "Probar la restricción FK.",
            BeltId = 999999, // inexistente a propósito
            CategoryId = (await db.TechniqueCategories.AsNoTracking().FirstAsync()).Id,
            PositionId = (await db.Positions.AsNoTracking().FirstAsync()).Id,
            IsActive = false
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        // Nada quedó persistido.
        Assert.False(await db.Techniques.AnyAsync(t => t.Name == "Técnica inválida para prueba"));
    }

    [Fact]
    public async Task Appsettings_Define_La_Base_Exclusiva_Sin_Otras_Bases()
    {
        // Verificación de CONFIGURACIÓN del proyecto web (lectura de archivo):
        // la conexión de la app sigue siendo la base exclusiva; las pruebas no
        // la usan (usan bases aisladas verificadas).
        var webProject = EncontrarProyectoWeb();
        Assert.True(webProject is not null, "No se encontró MiCaminoBJJ.Web/appsettings.json desde el directorio de pruebas.");

        var json = await File.ReadAllTextAsync(Path.Combine(webProject!, "appsettings.json"));

        // appsettings.json contiene comentarios // (aprobados por el parser de
        // configuración de .NET): se habilitan para leer con System.Text.Json.
        var opciones = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        using var documento = JsonDocument.Parse(json, opciones);

        var cadena = documento.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty(WebSqlServerOptions.ConnectionStringName)
            .GetString();

        Assert.NotNull(cadena);
        Assert.Contains($"Database={WebSqlServerOptions.DatabaseName}", cadena, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("(localdb)\\MSSQLLocalDB", cadena, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CryptoView", cadena, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SistemaEducativo", cadena, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MiCaminoBJJDb", cadena, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static async Task<List<string>> ConsultarStringsAsync(WebTfDbContext db, string sql)
    {
        var conexion = db.Database.GetDbConnection();

        // EF puede dejar la conexión abierta entre consultas manuales: solo
        // abrimos/cerramos si el estado lo requiere para no romper el ciclo de vida.
        var laAbrimosNosotros = conexion.State != System.Data.ConnectionState.Open;
        if (laAbrimosNosotros)
            await conexion.OpenAsync();

        try
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = sql;

            var resultados = new List<string>();
            await using var lector = await comando.ExecuteReaderAsync();
            while (await lector.ReadAsync())
                resultados.Add(lector.IsDBNull(0) ? string.Empty : lector.GetValue(0).ToString() ?? string.Empty);

            return resultados;
        }
        finally
        {
            if (laAbrimosNosotros)
                await conexion.CloseAsync();
        }
    }

    private static string? EncontrarProyectoWeb()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            var candidato = Path.Combine(directorio.FullName, "MiCaminoBJJ.Web", "appsettings.json");
            if (File.Exists(candidato))
                return Path.Combine(directorio.FullName, "MiCaminoBJJ.Web");

            directorio = directorio.Parent;
        }

        return null;
    }
}
