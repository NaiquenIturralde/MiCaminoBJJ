using Microsoft.EntityFrameworkCore;
using MiCaminoBJJ.Web.Persistence;

namespace MiCaminoBJJ.Web.Tests.Infrastructure;

/// <summary>
/// Utilidades de EF Core para bases de PRUEBA AISLADAS.
///
/// IMPORTANTE (Fase 3): este tipo NO crea ni elimina bases. El ciclo de vida
/// completo (crear desde migraciones y eliminar) vive en
/// <see cref="TestDatabaseLease"/>, que demuestra propiedad exclusiva antes de
/// cualquier operación destructiva. Así ninguna prueba puede borrar una base
/// preexistente —ni, por supuesto, la principal.
/// </summary>
public static class TestSqlServerDatabase
{
    /// <summary>
    /// Opciones de EF Core para una cadena YA validada por
    /// <see cref="TestDatabaseGuard"/>. Las migraciones se anclan al ensamblado web.
    /// </summary>
    public static DbContextOptions<WebTfDbContext> Opciones(string connectionString)
    {
        TestDatabaseGuard.EnsureIsolated(connectionString);

        return new DbContextOptionsBuilder<WebTfDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(WebTfDbContext).Assembly.GetName().Name))
            .Options;
    }
}

