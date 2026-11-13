using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Services;
using Xunit;

namespace MiCaminoBJJ.Web.Tests.Infrastructure;

/// <summary>
/// Base común de fixtures de integración sobre una base de PRUEBA AISLADA y
/// ÚNICA POR EJECUCIÓN (<see cref="TestDatabaseLease"/>): la base principal es
/// inalcanzable por diseño y no existe ninguna base "preexistente" que pueda
/// borrarse por error.
///
/// Ciclo de vida: <see cref="TestDatabaseLease.PrepareAsync"/> (verifica que la
/// base no existía y la crea desde las migraciones) y
/// <see cref="TestDatabaseLease.DropAsync"/> (valida la conexión efectiva y el
/// nombre exacto antes de eliminar).
/// </summary>
public abstract class BaseAisladaFixture : IAsyncLifetime
{
    private readonly bool _sembrar;

    protected BaseAisladaFixture(string sufijo, bool sembrar)
    {
        _sembrar = sembrar;

        Lease = TestDatabaseLease.Create(sufijo);
        ConnectionString = Lease.ConnectionString;
        DatabaseName = Lease.DatabaseName;
        Options = Lease.Options;
    }

    /// <summary>Arrendamiento de la base exclusiva de esta ejecución.</summary>
    public TestDatabaseLease Lease { get; }

    public string ConnectionString { get; }

    public string DatabaseName { get; }

    public DbContextOptions<WebTfDbContext> Options { get; }

    /// <summary>Motivo por el cual las pruebas no pueden ejecutarse (null = OK).</summary>
    public string? Error { get; private set; }

    public WebTfDbContext CreateContext() => Lease.CreateContext();

    /// <summary>
    /// Registra un identificador creado por las pruebas (trazabilidad).
    /// La limpieza es la eliminación COMPLETA de la base aislada al cierre: no
    /// se borran filas sueltas, así que no hace falta tracking por Id.
    /// </summary>
    public void Registrar(int id) => _ = id;

    public async Task InitializeAsync()
    {
        try
        {
            await Lease.PrepareAsync();

            if (_sembrar)
            {
                await using var db = CreateContext();
                TestDatabaseGuard.EnsureIsolated(db, DatabaseName);
                await WebLibrarySeeder.SeedAsync(db, NullLogger.Instance);
            }
        }
        catch (Exception ex)
        {
            Error = $"No se pudo preparar la base aislada '{DatabaseName}' "
                    + $"(sufijo '{Lease.SufijoUnico}'): {ex.Message}";
        }
    }

    /// <summary>Falla con mensaje claro cuando la preparación no fue posible.</summary>
    public void VerificarDisponibilidad()
    {
        Assert.True(
            Error is null,
            "Las pruebas de integración SQL Server no pueden ejecutarse. " + (Error ?? string.Empty));
    }

    public Task DisposeAsync() => Lease.DropAsync();
}

