using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MiCaminoBJJ.Web.Persistence.DesignTime;

/// <summary>
/// Design-time factory para generar migraciones SQL Server SIN abrir ninguna
/// base de datos ni servidor:
///
///   dotnet ef migrations add &lt;Nombre&gt; --project MiCaminoBJJ.Web --startup-project MiCaminoBJJ.Web --context WebTfDbContext
///
/// Se usa la cadena de ejemplo (LocalDB) únicamente como marcador de opciones;
/// `migrations add` solo compila el modelo y lo compara con el snapshot.
/// </summary>
public sealed class WebTfDbContextDesignTimeFactory : IDesignTimeDbContextFactory<WebTfDbContext>
{
    public WebTfDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WebTfDbContext>();
        WebSqlServerOptions.Configure(options, WebSqlServerOptions.ExampleConnectionString);
        return new WebTfDbContext(options.Options);
    }
}
