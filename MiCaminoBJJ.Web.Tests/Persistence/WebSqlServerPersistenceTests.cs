using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Infrastructure.Data;
using MiCaminoBJJ.Web.Identity;
using MiCaminoBJJ.Web.Persistence;

namespace MiCaminoBJJ.Web.Tests.Persistence;

/// <summary>
/// Etapa 2A — persistencia SQL Server de la web, SIN bases de datos reales.
/// Todas las verificaciones trabajan sobre opciones y modelos en memoria
/// (EF Core construye el modelo sin abrir conexiones).
/// </summary>
public class WebSqlServerPersistenceTests
{
    // ── Utilidades ───────────────────────────────────────────────────────

    private static WebTfDbContext CreateWebContext(string? connectionString = null)
    {
        var options = new DbContextOptionsBuilder<WebTfDbContext>();
        WebSqlServerOptions.Configure(options, connectionString ?? WebSqlServerOptions.ExampleConnectionString);
        return new WebTfDbContext(options.Options);
    }

    private static AppDbContext CreateSqliteContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:");
        return new AppDbContext(options.Options);
    }

    private static string FindWebAppsettingsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "MiCaminoBJJ.Web", "appsettings.json");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("No se encontró MiCaminoBJJ.Web/appsettings.json desde " + AppContext.BaseDirectory);
    }

    /// <summary>Verifica si existe una ruta de borrado en cascada entre dos tipos (grafo padre→hijo).</summary>
    private static bool HayCaminoDeCascada(Type desde, Type hasta, List<(Type Child, Type Parent)> cascadeEdges)
    {
        var visitados = new HashSet<Type> { desde };
        var cola = new Queue<Type>(new[] { desde });

        while (cola.Count > 0)
        {
            var actual = cola.Dequeue();
            foreach (var hijo in cascadeEdges.Where(e => e.Parent == actual).Select(e => e.Child))
            {
                if (hijo == hasta)
                    return true;
                if (visitados.Add(hijo))
                    cola.Enqueue(hijo);
            }
        }

        return false;
    }

    // ── Configuración del proveedor ──────────────────────────────────────

    [Fact]
    public void Proveedor_EsSqlServer_SinAbrirConexiones()
    {
        using var db = CreateWebContext();

        db.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.SqlServer");
        db.Model.GetEntityTypes().Should().NotBeEmpty("el modelo se construye en memoria, sin tocar servidores");
    }

    [Fact]
    public void GetConnectionString_SinClave_LanzaErrorConMensajeClaro()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var act = () => WebSqlServerOptions.GetConnectionString(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{WebSqlServerOptions.ConnectionStringName}*")
            .WithMessage("*ejemplo*");
    }

    [Fact]
    public void GetConnectionString_Vacia_LanzaError()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"ConnectionStrings:{WebSqlServerOptions.ConnectionStringName}"] = "   " })
            .Build();

        var act = () => WebSqlServerOptions.GetConnectionString(config);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Configure_ConCadenaVacia_LanzaError()
    {
        var options = new DbContextOptionsBuilder<WebTfDbContext>();

        var act = () => WebSqlServerOptions.Configure(options, "  ");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Appsettings_DefineBaseExclusivaDeLaWeb_SinCredenciales()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(FindWebAppsettingsPath(), optional: false)
            .Build();

        var connectionString = WebSqlServerOptions.GetConnectionString(config);

        connectionString.Should().Contain($"Database={WebSqlServerOptions.DatabaseName}");
        connectionString.Should().NotContain("Password=", "no deben versionarse credenciales reales");
        connectionString.Should().NotContain("pwd=");
    }

    [Fact]
    public void AddWebTfPersistence_RegistraElContexto_SinConectar()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{WebSqlServerOptions.ConnectionStringName}"] = WebSqlServerOptions.ExampleConnectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddWebTfPersistence(config);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WebTfDbContext>();

        db.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.SqlServer");
    }

    // ── Modelo reutilizado ───────────────────────────────────────────────

    [Fact]
    public void Modelo_SqlServer_ContieneLas20EntidadesDeDominio_MasLasDeIdentity()
    {
        using var web = CreateWebContext();
        using var maui = CreateSqliteContext();

        var webEntities = web.Model.GetEntityTypes().Select(e => e.ClrType).ToHashSet();
        var mauiEntities = maui.Model.GetEntityTypes().Select(e => e.ClrType).ToHashSet();

        // Etapa 4B: además de las 20 de dominio, el modelo WebTfDbContext
        // contiene exactamente las 7 entidades estándar de Identity.
        var entidadesIdentity = new[]
        {
            typeof(ApplicationUser),
            typeof(IdentityRole),
            typeof(IdentityUserRole<string>),
            typeof(IdentityUserClaim<string>),
            typeof(IdentityUserLogin<string>),
            typeof(IdentityUserToken<string>),
            typeof(IdentityRoleClaim<string>)
        };

        webEntities.IsSupersetOf(mauiEntities).Should().BeTrue(
            "la web debe conservar las 20 entidades de dominio de MAUI sin cambios");
        webEntities.Should().BeEquivalentTo(mauiEntities.Concat(entidadesIdentity),
            "20 de dominio + 7 estándar de Identity; ninguna entidad extra ni faltante");
        webEntities.Should().HaveCount(27);
    }

    [Fact]
    public void Modelo_SqlServer_UsaTiposPropiosDeSqlServer()
    {
        using var db = CreateWebContext();
        var model = db.Model;

        model.FindEntityType(typeof(AuditLog))!
            .FindProperty(nameof(AuditLog.TimestampUtc))!
            .GetColumnType().Should().Be("datetime2");

        model.FindEntityType(typeof(Technique))!
            .FindProperty(nameof(Technique.Description))!
            .GetColumnType().Should().Be("nvarchar(2000)");

        model.FindEntityType(typeof(TechniqueAlias))!
            .GetIndexes()
            .Should().Contain(i => i.IsUnique
                && i.GetAnnotations().Any(a => Equals(a.Value, "UX_TechniqueAliases_TechniqueId_NameNormalized")));
    }

    // ── Compatibilidad SQL Server: múltiples rutas en cascada ────────────

    [Fact]
    public void Modelo_SqlServer_NoTieneMultiplesRutasDeCascada()
    {
        using var db = CreateWebContext();

        var cascadeEdges = db.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys()
                .Where(fk => fk.DeleteBehavior == DeleteBehavior.Cascade)
                .Select(fk => (Child: fk.DeclaringEntityType.ClrType, Parent: fk.PrincipalEntityType.ClrType)))
            .ToList();

        var conflictos = new List<string>();

        foreach (var grupo in cascadeEdges.GroupBy(e => e.Child).Where(g => g.Count() > 1))
        {
            var padres = grupo.Select(e => e.Parent).Distinct().ToList();

            for (var i = 0; i < padres.Count; i++)
            {
                for (var j = i + 1; j < padres.Count; j++)
                {
                    if (HayCaminoDeCascada(padres[i], padres[j], cascadeEdges)
                        || HayCaminoDeCascada(padres[j], padres[i], cascadeEdges))
                    {
                        conflictos.Add($"{padres[i].Name} y {padres[j].Name} -> {grupo.Key.Name}");
                    }
                }
            }
        }

        conflictos.Should().BeEmpty("SQL Server rechaza las claves con múltiples rutas en cascada (error 1785)");
    }

    [Fact]
    public void Modelo_SqlServer_DifiereDelDeMAUI_SoloEnTrainingSessionId()
    {
        using var web = CreateWebContext();
        using var maui = CreateSqliteContext();

        var diferencias = new List<string>();

        foreach (var webEntity in web.Model.GetEntityTypes())
        {
            var mauiEntity = maui.Model.FindEntityType(webEntity.ClrType);

            // Etapa 4B: las entidades nuevas de Identity no existen en MAUI;
            // esta prueba compara SOLO el modelo de dominio compartido.
            if (mauiEntity is null)
                continue;

            foreach (var fk in webEntity.GetForeignKeys())
            {
                var mauiFk = mauiEntity.GetForeignKeys().FirstOrDefault(f =>
                    f.PrincipalEntityType.ClrType == fk.PrincipalEntityType.ClrType
                    && f.Properties.Select(p => p.Name).SequenceEqual(fk.Properties.Select(p => p.Name)));

                if (mauiFk is not null && mauiFk.DeleteBehavior != fk.DeleteBehavior)
                    diferencias.Add($"{webEntity.ClrType.Name}.{fk.Properties[0].Name}: SQLite={mauiFk.DeleteBehavior} / SQL Server={fk.DeleteBehavior}");
            }
        }

        diferencias.Should().ContainSingle()
            .Which.Should().Be(
                "TechniquePractice.TrainingSessionId: SQLite=Cascade / SQL Server=Restrict",
                "es el único ajuste específico de proveedor documentado en la Etapa 2A");
    }

    // ── Migraciones independientes ───────────────────────────────────────

    [Fact]
    public void MigracionesSqlServer_VivenEnElEnsambladoDeLaWeb()
    {
        using var db = CreateWebContext();

        var migrations = db.Database.GetMigrations().ToList();

        migrations.Should().HaveCount(2,
            "InitialSqlServer + AddIdentity (Etapa 4B, la segunda SIN aplicar todavía)");
        migrations.Should().Contain(m => m.EndsWith("_InitialSqlServer", StringComparison.Ordinal));
        migrations.Should().Contain(m => m.EndsWith("_AddIdentity", StringComparison.Ordinal));
        typeof(WebTfDbContext).Assembly.GetName().Name.Should().NotBe(
            typeof(AppDbContext).Assembly.GetName().Name,
            "las migraciones SQL Server no pueden compartir ensamblado con las de SQLite");
    }

    [Fact]
    public void MigracionesSqlServer_NoIncluyeLasDeSQLiteNiAlReves()
    {
        using var web = CreateWebContext();
        using var maui = CreateSqliteContext();

        var webMigrations = web.Database.GetMigrations().ToList();
        var mauiMigrations = maui.Database.GetMigrations().ToList();

        webMigrations.Should().NotContain(m => m.Contains("InitialCreate", StringComparison.Ordinal));
        mauiMigrations.Should().Contain(m => m.Contains("InitialCreate", StringComparison.Ordinal));
        mauiMigrations.Should().NotContain(m => m.Contains("InitialSqlServer", StringComparison.Ordinal));
        mauiMigrations.Should().HaveCount(3, "las tres migraciones SQLite existentes siguen intactas");
    }

    [Fact]
    public void EnsambladoDeInfrastructure_NoContieneLaMigracionSqlServer()
    {
        var migracionesEnInfrastructure = typeof(AppDbContext).Assembly
            .GetTypes()
            .Where(t => typeof(Microsoft.EntityFrameworkCore.Migrations.Migration).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.Name)
            .ToList();

        migracionesEnInfrastructure.Should().NotContain(n => n.Contains("InitialSqlServer"));
    }
}
