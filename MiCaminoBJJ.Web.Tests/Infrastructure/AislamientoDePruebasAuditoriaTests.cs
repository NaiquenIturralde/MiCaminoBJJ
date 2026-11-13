using System.Text.RegularExpressions;
using FluentAssertions;

namespace MiCaminoBJJ.Web.Tests.Infrastructure;

/// <summary>
/// FASE 4 — auditoría AUTOMÁTICA del código de pruebas (sin SQL Server).
///
/// Estas pruebas no abren ninguna conexión: leen los archivos .cs del proyecto
/// de pruebas y exigen que:
/// 1. Toda cadena/canal de configuración que se use para ESCRIBIR apunte a una
///    base aislada (nunca a la principal).
/// 2. Las únicas apariciones del nombre de la base principal sean negativas
///    (pruebas de rechazo) o parte del propio prefijo de pruebas.
/// 3. Ninguna rutina de limpieza use DROP/TRUNCATE ni borre filas sin pasar
///    por <see cref="TestDatabaseGuard"/>.
///
/// Si alguien reintroduce en el futuro una conexión a la base real, estas
/// pruebas fallan ANTES de que se ejecute ninguna prueba de integración.
/// </summary>
public class AislamientoDePruebasAuditoriaTests
{
    private static readonly string RaizProyectoTests = EncontrarRaiz();

    private static string EncontrarRaiz()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "MiCaminoBJJ.Web.Tests.csproj")))
                return directorio.FullName;
            directorio = directorio.Parent;
        }

        throw new DirectoryNotFoundException(
            "No se encontró MiCaminoBJJ.Web.Tests.csproj desde " + AppContext.BaseDirectory);
    }

    /// <summary>Archivos .cs del proyecto de pruebas (sin bin/obj ni este archivo).</summary>
    private static IEnumerable<(string Ruta, string Texto)> ArchivosFuente()
    {
        var propio = nameof(AislamientoDePruebasAuditoriaTests) + ".cs";

        return Directory
            .EnumerateFiles(RaizProyectoTests, "*.cs", SearchOption.AllDirectories)
            .Where(ruta => !ruta.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !ruta.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !Path.GetFileName(ruta).Equals(propio, StringComparison.OrdinalIgnoreCase))
            .Select(ruta => (ruta, File.ReadAllText(ruta)));
    }

    /// <summary>Rutas relativas permitidas para nombrar la base principal.</summary>
    private static readonly string[] ArchivosQuePuedenNombrarLaPrincipal =
    [
        "TestDatabaseGuard.cs",
        "TestDatabaseGuardTests.cs",
        "AislamientoDePruebasAuditoriaTests.cs",
        "AdminBootstrapTests.cs", // pruebas NEGATIVAS de rechazo de servidor/base
    ];

    [Fact]
    public void NingunArchivoDePruebas_ApuntaALaBasePrincipal()
    {
        var infracciones = ArchivosFuente()
            .Where(a => !ArchivosQuePuedenNombrarLaPrincipal.Contains(Path.GetFileName(a.Ruta)))
            // Cadenas que declaran explícitamente la base principal dentro de una
            // construcción de conexión (Database= / InitialCatalog=).
            .Where(a => Regex.IsMatch(
                a.Texto,
                $@"(Database|Initial\s*Catalog)\s*=\s*{Regex.Escape(TestDatabaseGuard.MainDatabaseName)}\s*(;|""|$)",
                RegexOptions.IgnoreCase))
            .Select(a => Path.GetRelativePath(RaizProyectoTests, a.Ruta))
            .ToList();

        infracciones.Should().BeEmpty(
            "ninguna prueba puede construir una conexión con Database="
            + TestDatabaseGuard.MainDatabaseName
            + ". Infracciones: " + string.Join(", ", infracciones));
    }

    [Fact]
    public void NingunArchivoDePruebas_EjecutaComandosDestructivosContraLaPrincipal()
    {
        // DROP DATABASE / TRUNCATE / ALTER DATABASE son prohibidos en TODO el
        // proyecto de pruebas: la limpieza autorizada es EnsureDeletedAsync del
        // propio arrendamiento (validado antes) y los DELETE acotados por guarda.
        var prohibidos = new[] { "DROP DATABASE", "TRUNCATE TABLE", "ALTER DATABASE" };

        var infracciones = ArchivosFuente()
            .SelectMany(a => prohibidos
                .Where(p => a.Texto.Contains(p, StringComparison.OrdinalIgnoreCase))
                .Select(p => $"{Path.GetRelativePath(RaizProyectoTests, a.Ruta)} → {p}"))
            .ToList();

        infracciones.Should().BeEmpty(
            "no se permiten comandos destructivos de servidor en pruebas: " + string.Join(", ", infracciones));
    }

    [Fact]
    public void TodaRutinaDeBorradoDeFilas_EstaPrecedidaPorLaGuarda()
    {
        // Cada archivo que ejecuta DELETE FROM debe invocar TestDatabaseGuard
        // (como mínimo una vez) para demostrar el destino antes de limpiar.
        var archivosConDelete = ArchivosFuente()
            .Where(a => a.Texto.Contains("DELETE FROM", StringComparison.OrdinalIgnoreCase));

        foreach (var (ruta, texto) in archivosConDelete)
        {
            texto.Should().Contain(nameof(TestDatabaseGuard),
                $"{Path.GetRelativePath(RaizProyectoTests, ruta)} ejecuta DELETE FROM y debe validar el destino con TestDatabaseGuard");
        }
    }

    [Fact]
    public void TodoEnsureDeleted_UsaLaGuardaONumeroDeArrendamiento()
    {
        // EnsureDeletedAsync debe estar precedido por la guarda en el MISMO archivo.
        var archivosConEnsureDeleted = ArchivosFuente()
            .Where(a => a.Texto.Contains("EnsureDeleted", StringComparison.OrdinalIgnoreCase));

        foreach (var (ruta, texto) in archivosConEnsureDeleted)
        {
            texto.Should().Contain(nameof(TestDatabaseGuard),
                $"{Path.GetRelativePath(RaizProyectoTests, ruta)} elimina una base y debe validarla con TestDatabaseGuard");

            // El único nombre permitido es el del arrendamiento (nunca uno fijo).
            texto.Should().Contain(nameof(TestDatabaseLease),
                $"{Path.GetRelativePath(RaizProyectoTests, ruta)} debe operar sobre el arrendamiento propio de esta ejecución");
        }
    }

    [Fact]
    public void ElProyectoDePruebas_NoCopiaAppsettingsComoFuenteDeConexion()
    {
        // appsettings.json se copia al directorio de salida (por la referencia al
        // proyecto web), pero NINGUNA prueba debe leer la cadena desde ahí para
        // conectarse. Se exige que no exista AddJsonFile de la configuración web
        // salvo en la prueba que solo LEE el archivo como texto.
        var lectores = ArchivosFuente()
            .Where(a => a.Texto.Contains("AddJsonFile(", StringComparison.OrdinalIgnoreCase))
            .Select(a => Path.GetRelativePath(RaizProyectoTests, a.Ruta))
            .ToList();

        lectores.Should().OnlyContain(r => r.EndsWith("WebSqlServerPersistenceTests.cs", StringComparison.OrdinalIgnoreCase),
            "solo la prueba de CONFIGURACIÓN puede leer appsettings.json (y nunca para conectarse). " +
            "Archivos: " + string.Join(", ", lectores));
    }
}
