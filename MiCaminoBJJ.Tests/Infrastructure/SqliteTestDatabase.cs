using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Tests.Infrastructure;

/// <summary>
/// Base de pruebas con SQLite real en memoria ("DataSource=:memory:").
/// La conexión se mantiene abierta durante todo el test (si no, la base desaparece).
/// NO toca ninguna base de datos real de la aplicación.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    public SqliteConnection Connection { get; }
    public AppDbContext Context { get; }

    public SqliteTestDatabase()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(Connection)
            .Options;

        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(Connection)
            .Options;
        return new AppDbContext(options);
    }

    public void Execute(string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        Context.Dispose();
        Connection.Dispose();
    }
}
