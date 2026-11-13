using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MiCaminoBJJ.Web.Persistence;

/// <summary>
/// Registro de la persistencia SQL Server de la web (Etapa 2A, activa desde 2B).
///
/// IMPORTANTE: este registro NO toca ninguna base de datos.
/// - No llama a <c>Database.EnsureCreated</c>, <c>EnsureDeleted</c> ni <c>Migrate()</c>.
/// - No ejecuta seeders (el de la biblioteca se invoca aparte en Program.cs,
///   solo en Development, de forma idempotente).
/// - No registra <c>AddInfrastructure</c> (que inicializa SQLite).
/// EF Core construye el modelo en memoria; la conexión se abre cuando algo
/// solicite datos (desde la Etapa 2B: la biblioteca vía
/// <c>TecnicasSqlServerConsultaService</c>).
/// </summary>
public static class WebPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddWebTfPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Falla temprano y con mensaje claro si falta la configuración.
        var connectionString = WebSqlServerOptions.GetConnectionString(configuration);

        services.AddDbContext<WebTfDbContext>(options => WebSqlServerOptions.Configure(options, connectionString));

        return services;
    }
}
