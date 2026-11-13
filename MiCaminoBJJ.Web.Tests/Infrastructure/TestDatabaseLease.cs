using Microsoft.EntityFrameworkCore;
using MiCaminoBJJ.Web.Persistence;

namespace MiCaminoBJJ.Web.Tests.Infrastructure;

/// <summary>
/// Arrendamiento de UNA base de pruebas AISLADA, ÚNICA POR EJECUCIÓN.
///
/// Fase 3 (seguridad de limpieza):
/// - El nombre incluye un identificador de corrida + contador: dos ejecuciones
///   (o dos pruebas en paralelo) nunca comparten base, y no existen "bases
///   preexistentes" que se puedan borrar por error.
/// - Antes de crear se verifica que la base NO exista: si existiera, la
///   propiedad exclusiva no está demostrada y se aborta sin borrar nada.
/// - Antes de eliminar se valida la CONEXIÓN EFECTIVA y además que el nombre
///   sea EXACTAMENTE el de este arrendamiento.
/// - La limpieza solo puede afectar al recurso creado por esta ejecución.
///
/// Todas las operaciones pasan por <see cref="TestDatabaseGuard"/>.
/// </summary>
public sealed class TestDatabaseLease
{
    private static readonly string IdCorrida = "r" + Guid.NewGuid().ToString("N")[..8];
    private static int _contador;

    private readonly object _candado = new();
    private bool _liberada;

    private TestDatabaseLease(string sufijo, string sufijoUnico)
    {
        Sufijo = sufijo;
        SufijoUnico = sufijoUnico;
        ConnectionString = TestDatabaseGuard.BuildIsolated(sufijoUnico);
        DatabaseName = TestDatabaseGuard.EnsureIsolated(ConnectionString);
        Options = TestSqlServerDatabase.Opciones(ConnectionString);
    }

    /// <summary>Sufijo lógico solicitado (p. ej. "Pipeline").</summary>
    public string Sufijo { get; }

    /// <summary>Sufijo realmente usado: incluye corrida + contador.</summary>
    public string SufijoUnico { get; }

    /// <summary>Cadena de conexión validada por la guarda.</summary>
    public string ConnectionString { get; }

    /// <summary>Nombre de la base (validado: prefijo de pruebas, nunca principal).</summary>
    public string DatabaseName { get; }

    /// <summary>Opciones EF ya validadas.</summary>
    public DbContextOptions<WebTfDbContext> Options { get; }

    /// <summary>
    /// Crea un arrendamiento con nombre único por ejecución. NO crea la base:
    /// eso lo hace <see cref="PrepareAsync"/> tras demostrar que no existía.
    /// </summary>
    public static TestDatabaseLease Create(string sufijo)
    {
        if (string.IsNullOrWhiteSpace(sufijo))
            throw new ArgumentException("El sufijo de la base de pruebas es obligatorio.", nameof(sufijo));

        var limpio = sufijo.Trim();
        var numero = Interlocked.Increment(ref _contador);
        return new TestDatabaseLease(limpio, $"{limpio}_{IdCorrida}_{numero}");
    }

    /// <summary>Contexto EF sobre la base de este arrendamiento.</summary>
    public WebTfDbContext CreateContext() => new(Options);

    /// <summary>
    /// Crea el esquema desde las migraciones del proyecto web. Antes exige que
    /// la base NO exista (propiedad exclusiva demostrada).
    /// </summary>
    public async Task PrepareAsync()
    {
        await using var db = CreateContext();
        TestDatabaseGuard.EnsureIsolated(db, DatabaseName);

        if (await db.Database.CanConnectAsync())
        {
            throw new InvalidOperationException(
                $"BLOQUEADO: la base '{DatabaseName}' ya existía antes de esta ejecución. "
                + "No se elimina ni se reutiliza: la propiedad exclusiva no está demostrada.");
        }

        await db.Database.MigrateAsync();
        TestDatabaseGuard.EnsureIsolated(db, DatabaseName);
    }

    /// <summary>Versión sincrónica de <see cref="PrepareAsync"/> (arranque de fábricas).</summary>
    public void Prepare()
    {
        using var db = CreateContext();
        TestDatabaseGuard.EnsureIsolated(db, DatabaseName);

        if (db.Database.CanConnect())
        {
            throw new InvalidOperationException(
                $"BLOQUEADO: la base '{DatabaseName}' ya existía antes de esta ejecución. "
                + "No se elimina ni se reutiliza: la propiedad exclusiva no está demostrada.");
        }

        db.Database.Migrate();
        TestDatabaseGuard.EnsureIsolated(db, DatabaseName);
    }

    /// <summary>
    /// Elimina la base SOLO si la conexión efectiva es la de este arrendamiento.
    /// Un fallo de limpieza se propaga (nunca se silencia).
    /// </summary>
    public async Task DropAsync()
    {
        lock (_candado)
        {
            if (_liberada)
                return;
            _liberada = true;
        }

        try
        {
            await using var db = CreateContext();

            // Propiedad exclusiva: servidor autorizado + nombre EXACTO del lease.
            TestDatabaseGuard.EnsureIsolated(db, DatabaseName);
            await db.Database.EnsureDeletedAsync();

            if (await db.Database.CanConnectAsync())
            {
                throw new InvalidOperationException(
                    $"La base aislada '{DatabaseName}' sigue existiendo tras la limpieza.");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"La limpieza de la base aislada '{DatabaseName}' falló. Revise manualmente. "
                + "Este residuo NO debe borrarse a mano sin comprobar que es una base de pruebas.",
                ex);
        }
    }

    /// <summary>Versión sincrónica (Dispose de fábricas de xUnit).</summary>
    public void Drop() => DropAsync().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync() => await DropAsync();
}
