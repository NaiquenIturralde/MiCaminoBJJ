using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MiCaminoBJJ.Web.Tests.Infrastructure;

/// <summary>
/// Guarda OBLIGATORIA de aislamiento de las pruebas automatizadas.
///
/// Motivo: una auditoría detectó que varios fixtures usaban la base PRINCIPAL
/// (<c>MiCaminoBJJ_Web_TFI_2026</c>) y que sus rutinas de limpieza borraban
/// cuentas reales (administrador de Identity y practicantes).
///
/// Reglas que impone esta guarda a TODA cadena de conexión de pruebas:
/// 1. Servidor exactamente <c>(localdb)\MSSQLLocalDB</c>.
/// 2. La base declarada NO puede ser la principal.
/// 3. La base declarada NO puede ser una base de sistema (master/model/msdb/tempdb).
/// 4. La base declarada debe empezar con <c>MiCaminoBJJ_Web_TFI_2026_Test</c>.
/// 5. La cadena debe declarar <c>Database</c>/<c>InitialCatalog</c>.
/// 6. El sufijo debe ser seguro (solo letras, dígitos y guión bajo): impide
///    inyectar claves adicionales en la cadena construida.
///
/// Se valida DOS veces: al construir la conexión y —en defensa en profundidad—
/// contra la CONEXIÓN EFECTIVA de EF Core justo antes de cualquier escritura
/// o limpieza (<see cref="EnsureIsolated(DbContext, string?)"/>).
/// </summary>
public static class TestDatabaseGuard
{
    /// <summary>Servidor único autorizado para pruebas.</summary>
    public const string AuthorizedServer = @"(localdb)\MSSQLLocalDB";

    /// <summary>Base PRINCIPAL protegida: jamás puede ser destino de pruebas.</summary>
    public const string MainDatabaseName = "MiCaminoBJJ_Web_TFI_2026";

    /// <summary>Prefijo obligatorio de toda base de pruebas.</summary>
    public const string TestDatabasePrefix = "MiCaminoBJJ_Web_TFI_2026_Test";

    /// <summary>Sufijo válido: sin ';', '=' ni espacios (evita inyección de claves).</summary>
    private static readonly Regex SufijoSeguro = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    /// <summary>Bases del sistema: nunca pueden ser destino de una prueba.</summary>
    private static readonly HashSet<string> BasesDelSistema = new(StringComparer.OrdinalIgnoreCase)
    {
        "master", "model", "msdb", "tempdb"
    };

    /// <summary>
    /// Construye una cadena de conexión hacia una base de pruebas AISLADA y
    /// verifica el resultado antes de devolverlo.
    /// </summary>
    /// <param name="suffix">Sufijo descriptivo (p. ej. "CuentaRegistro").</param>
    public static string BuildIsolated(string suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
            throw new ArgumentException("El sufijo de la base de pruebas es obligatorio.", nameof(suffix));

        var limpio = suffix.Trim();
        if (!SufijoSeguro.IsMatch(limpio))
            throw new ArgumentException(
                "El sufijo de la base de pruebas solo admite letras, dígitos y guión bajo.", nameof(suffix));

        var nombre = TestDatabasePrefix + limpio;
        EnsureIsolatedDatabaseName(nombre);

        var cadena = $"Server={AuthorizedServer};Database={nombre};Trusted_Connection=True;"
                   + "MultipleActiveResultSets=true;TrustServerCertificate=True";

        // Doble verificación sobre lo realmente emitido (no sobre la entrada).
        EnsureIsolated(cadena);
        return cadena;
    }

    /// <summary>
    /// Valida una cadena de conexión y devuelve el nombre de la base (para
    /// trazabilidad). Lanza <see cref="InvalidOperationException"/> si el
    /// destino no está demostrado como aislado.
    /// </summary>
    public static string EnsureIsolated(string? connectionString)
    {
        var builder = Parse(connectionString);
        EnsureIsolatedDatabaseName(builder.InitialCatalog);
        return builder.InitialCatalog;
    }

    /// <summary>
    /// Defensa en profundidad: valida la CONEXIÓN EFECTIVA del contexto antes
    /// de escribir o limpiar. Si se indica <paramref name="nombreEsperado"/>,
    /// además exige que sea EXACTAMENTE esa base (propiedad exclusiva del
    /// recurso que se va a borrar).
    /// </summary>
    public static string EnsureIsolated(DbContext db, string? nombreEsperado = null)
    {
        ArgumentNullException.ThrowIfNull(db);

        var conexion = db.Database.GetDbConnection();
        var nombre = conexion.Database;

        if (string.IsNullOrWhiteSpace(nombre))
            throw new InvalidOperationException("BLOQUEADO: la conexión efectiva no declara base de datos.");

        EnsureIsolatedDatabaseName(nombre);

        // El DataSource de la conexión abierta también debe ser el autorizado.
        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(conexion.ConnectionString ?? string.Empty);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "BLOQUEADO: la conexión efectiva no es interpretable por SqlConnectionStringBuilder.", ex);
        }

        if (string.IsNullOrWhiteSpace(builder.DataSource))
            throw new InvalidOperationException(
                "BLOQUEADO: la conexión efectiva no declara servidor (DataSource).");

        if (!string.Equals(builder.DataSource, AuthorizedServer, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"BLOQUEADO: la conexión efectiva usa el servidor '{builder.DataSource}'. "
                + $"Solo se permite {AuthorizedServer}.");
        }

        if (!string.IsNullOrWhiteSpace(nombreEsperado)
            && !string.Equals(nombre, nombreEsperado, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"BLOQUEADO: la conexión efectiva apunta a '{nombre}' y no al recurso propio "
                + $"'{nombreEsperado}'. No se ejecuta ninguna operación destructiva.");
        }

        return nombre;
    }

    /// <summary>true si el nombre corresponde a la base PRINCIPAL protegida.</summary>
    public static bool EsBasePrincipal(string? databaseName) =>
        string.Equals(databaseName, MainDatabaseName, StringComparison.OrdinalIgnoreCase);

    /// <summary>true si el nombre corresponde a una base de sistema de SQL Server.</summary>
    public static bool EsBaseDelSistema(string? databaseName) =>
        !string.IsNullOrWhiteSpace(databaseName) && BasesDelSistema.Contains(databaseName);

    /// <summary>true si el nombre respeta el prefijo obligatorio de pruebas.</summary>
    public static bool EsBaseDePruebas(string? databaseName) =>
        !string.IsNullOrWhiteSpace(databaseName)
        && databaseName.StartsWith(TestDatabasePrefix, StringComparison.OrdinalIgnoreCase)
        && !EsBasePrincipal(databaseName)
        && !EsBaseDelSistema(databaseName);

    // ── Internos ──────────────────────────────────────────────────────────

    private static SqlConnectionStringBuilder Parse(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "BLOQUEADO: cadena de conexión vacía. Las pruebas exigen una base aislada explícita.");

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "BLOQUEADO: la cadena de conexión no es interpretable por SqlConnectionStringBuilder.", ex);
        }

        if (string.IsNullOrWhiteSpace(builder.DataSource))
            throw new InvalidOperationException("BLOQUEADO: la cadena no declara servidor (DataSource).");

        if (!string.Equals(builder.DataSource, AuthorizedServer, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"BLOQUEADO: el servidor '{builder.DataSource}' no está autorizado para pruebas. "
                + $"Solo se permite {AuthorizedServer}.");

        return builder;
    }

    private static void EnsureIsolatedDatabaseName(string? databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException(
                "BLOQUEADO: la cadena no declara Database (InitialCatalog).");

        if (EsBaseDelSistema(databaseName))
            throw new InvalidOperationException(
                $"BLOQUEADO: la cadena apunta a la base de sistema '{databaseName}'. "
                + "Ninguna prueba puede operar sobre bases del servidor.");

        if (EsBasePrincipal(databaseName))
            throw new InvalidOperationException(
                $"BLOQUEADO: la cadena apunta a la base PRINCIPAL '{MainDatabaseName}'. "
                + "Ninguna prueba ni limpieza puede operar sobre datos reales.");

        if (!databaseName.StartsWith(TestDatabasePrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"BLOQUEADO: la base '{databaseName}' no es una base de pruebas verificada. "
                + $"Debe empezar con '{TestDatabasePrefix}'.");
    }
}
