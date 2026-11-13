using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MiCaminoBJJ.Web.Persistence;

/// <summary>
/// Configuración SQL Server de la web (Etapa 2A).
///
/// - Base EXCLUSIVA de la web: <see cref="DatabaseName"/> (MiCaminoBJJ_Web_TFI_2026),
///   sin relación con la base SQLite de la app móvil (%LocalAppData%\MiCaminoBJJ\micaminobjj.db).
/// - Sin credenciales: la cadena de ejemplo usa autenticación de Windows/LocalDB.
///   Cualquier credencial real se agrega SOLO en user-secrets/variables de entorno,
///   nunca en appsettings.json versionado.
/// - Las migraciones se generan/aplican SOLO contra esta base y viven en este
///   ensamblado (Migrations/SqlServer), separadas de las de SQLite.
/// </summary>
public static class WebSqlServerOptions
{
    /// <summary>Nombre de la clave en ConnectionStrings.</summary>
    public const string ConnectionStringName = "MiCaminoBJJWeb";

    /// <summary>Nombre exclusivo de la base de datos web.</summary>
    public const string DatabaseName = "MiCaminoBJJ_Web_TFI_2026";

    /// <summary>
    /// Cadena de EJEMPLO sin credenciales. Sirve para arrancar la app y para
    /// `dotnet ef migrations add` (que no abre conexión a ningún servidor).
    /// </summary>
    public const string ExampleConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=" + DatabaseName
        + ";Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    /// <summary>
    /// Lee y valida la cadena de conexión. Errores de configuración explícitos:
    /// una cadena ausente o vacía lanza <see cref="InvalidOperationException"/>
    /// con el nombre de la clave y un ejemplo, en lugar de fallar tarde.
    /// </summary>
    public static string GetConnectionString(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var value = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Configuración inválida: falta ConnectionStrings:{ConnectionStringName}. "
                + $"Definila en appsettings.json, user-secrets o variables de entorno. "
                + $"Ejemplo sin credenciales: \"{ExampleConnectionString}\". "
                + "La base de la web (MiCaminoBJJ_Web_TFI_2026) es independiente de la SQLite de la app móvil.");
        }

        return value.Trim();
    }

    /// <summary>
    /// Aplica el proveedor SQL Server. Las migraciones se anclan a ESTE ensamblado
    /// (MiCaminoBJJ.Web) para que jamás se mezclen con las de SQLite, que viven
    /// en MiCaminoBJJ.Infrastructure.
    /// </summary>
    public static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"Cadena de conexión vacía para '{ConnectionStringName}': no se puede configurar SQL Server.");

        options.UseSqlServer(connectionString, sql =>
        {
            // Migraciones independientes de SQLite: mismo DbContextType, otro ensamblado.
            sql.MigrationsAssembly(typeof(WebTfDbContext).Assembly.GetName().Name);
        });
    }
}
