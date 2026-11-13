using FluentAssertions;
using MiCaminoBJJ.Web.Tests.Infrastructure;

namespace MiCaminoBJJ.Web.Tests.Infrastructure;

/// <summary>
/// Fase 4 — pruebas UNITARIAS del <see cref="TestDatabaseGuard"/>, sin conexión
/// a SQL Server: solo interpretación/validación de cadenas.
/// </summary>
public class TestDatabaseGuardTests
{
    // ── Rechazos obligatorios ─────────────────────────────────────────────

    [Fact]
    public void Rechaza_LaBasePrincipal()
    {
        var cadena = $"Server=(localdb)\\MSSQLLocalDB;Database={TestDatabaseGuard.MainDatabaseName};Trusted_Connection=True";

        var act = () => TestDatabaseGuard.EnsureIsolated(cadena);

        act.Should().Throw<InvalidOperationException>().WithMessage("*PRINCIPAL*");
    }

    [Fact]
    public void Rechaza_ServidorIncorrecto()
    {
        var cadena = $"Server=localhost;Database={TestDatabaseGuard.TestDatabasePrefix}X;Trusted_Connection=True";

        var act = () => TestDatabaseGuard.EnsureIsolated(cadena);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no está autorizado*");
    }

    [Fact]
    public void Rechaza_BaseSinPrefijoAutorizado()
    {
        var cadena = "Server=(localdb)\\MSSQLLocalDB;Database=MiCaminoBJJ_Web_TFI_2026_InitAdminTest;Trusted_Connection=True";

        var act = () => TestDatabaseGuard.EnsureIsolated(cadena);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no es una base de pruebas verificada*");
    }

    [Fact]
    public void Rechaza_ConexionSinDatabase()
    {
        var cadena = "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True";

        var act = () => TestDatabaseGuard.EnsureIsolated(cadena);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no declara Database*");
    }

    [Fact]
    public void Rechaza_ConfiguracionVacia()
    {
        foreach (var cadena in new[] { null, "", "   " })
        {
            var act = () => TestDatabaseGuard.EnsureIsolated(cadena);
            act.Should().Throw<InvalidOperationException>();
        }
    }

    [Fact]
    public void Rechaza_SufijoVacio_AlConstruir()
    {
        var act = () => TestDatabaseGuard.BuildIsolated("  ");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("master")]
    [InlineData("model")]
    [InlineData("msdb")]
    [InlineData("tempdb")]
    public void Rechaza_BasesDelSistema(string baseSistema)
    {
        var cadena = $"Server=(localdb)\\MSSQLLocalDB;Database={baseSistema};Trusted_Connection=True";

        var act = () => TestDatabaseGuard.EnsureIsolated(cadena);

        act.Should().Throw<InvalidOperationException>().WithMessage("*base de sistema*");
    }

    [Theory]
    [InlineData("X;Server=localhost")]
    [InlineData("X;Database=MiCaminoBJJ_Web_TFI_2026")]
    [InlineData("X Y")]
    [InlineData("X=y")]
    public void Rechaza_SufijosQueIntentanInyectarClaves(string sufijo)
    {
        var act = () => TestDatabaseGuard.BuildIsolated(sufijo);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rechaza_CadenaApuntandoAServidorRemoto_ConBaseDePruebasValida()
    {
        // El nombre es válido, pero el servidor no: el rechazo debe venir del
        // servidor, no solo del nombre de la base.
        var cadena = $"Server=192.168.1.50;Database={TestDatabaseGuard.TestDatabasePrefix}Ok;Trusted_Connection=True";

        var act = () => TestDatabaseGuard.EnsureIsolated(cadena);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no está autorizado*");
    }

    [Fact]
    public void Rechaza_NombreParecidoALaPrincipal_PeroSinPrefijoExacto()
    {
        // "MiCaminoBJJ_Web_TFI_2026X" no tiene el prefijo de pruebas exigido.
        var cadena = "Server=(localdb)\\MSSQLLocalDB;Database=MiCaminoBJJ_Web_TFI_2026X;Trusted_Connection=True";

        var act = () => TestDatabaseGuard.EnsureIsolated(cadena);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no es una base de pruebas verificada*");
    }

    // ── Aceptación estricta ──────────────────────────────────────────────

    [Fact]
    public void Acepta_SoloBasesAisladas_Validas()
    {
        var cadena = TestDatabaseGuard.BuildIsolated("CuentaRegistro");

        var nombre = TestDatabaseGuard.EnsureIsolated(cadena);

        nombre.Should().StartWith(TestDatabaseGuard.TestDatabasePrefix);
        TestDatabaseGuard.EsBasePrincipal(nombre).Should().BeFalse();
        TestDatabaseGuard.EsBaseDePruebas(nombre).Should().BeTrue();
    }

    [Fact]
    public void BuildIsolated_IncluyeServidorAutorizado()
    {
        var cadena = TestDatabaseGuard.BuildIsolated("Pipeline");
        cadena.Should().Contain("(localdb)\\MSSQLLocalDB");
        cadena.Should().NotContain(TestDatabaseGuard.MainDatabaseName + ";");
    }

    [Fact]
    public void EsBaseDePruebas_NoAceptaLaPrincipal()
    {
        TestDatabaseGuard.EsBaseDePruebas(TestDatabaseGuard.MainDatabaseName).Should().BeFalse();
        TestDatabaseGuard.EsBaseDePruebas(TestDatabaseGuard.MainDatabaseName + "_Test").Should().BeTrue();
        TestDatabaseGuard.EsBaseDePruebas(null).Should().BeFalse();
    }

    [Fact]
    public void EsBaseDePruebas_NoAceptaBasesDelSistema()
    {
        foreach (var sistema in new[] { "master", "model", "msdb", "tempdb" })
            TestDatabaseGuard.EsBaseDePruebas(sistema).Should().BeFalse($"{sistema} es base del servidor");
    }

    // ── Configuración heredada de la aplicación ───────────────────────────

    [Theory]
    [MemberData(nameof(CadenasHeredadasDeLaAplicacion))]
    public void Rechaza_LaCadenaDeLaAplicacion_NuncaSirveParaPruebas(string cadena)
    {
        // Ninguna configuración heredada de appsettings.json (la base principal)
        // puede usarse para pruebas: la guarda la rechaza explícitamente.
        var act = () => TestDatabaseGuard.EnsureIsolated(cadena);

        act.Should().Throw<InvalidOperationException>().WithMessage("*PRINCIPAL*");
    }

    public static IEnumerable<object[]> CadenasHeredadasDeLaAplicacion()
    {
        // Variante 1: la cadena de EJEMPLO que publica el proyecto web.
        yield return new object[] { MiCaminoBJJ.Web.Persistence.WebSqlServerOptions.ExampleConnectionString };

        // Variante 2: la misma base principal expresada con "Initial Catalog"
        // y orden distinto (típico en configuraciones heredadas copiadas).
        yield return new object[]
        {
            "Trusted_Connection=True;Initial Catalog=" + TestDatabaseGuard.MainDatabaseName
            + ";Server=(localdb)\\MSSQLLocalDB;TrustServerCertificate=True"
        };
    }

    // ── Arrendamientos únicos por ejecución (sin abrir conexiones) ────────

    [Fact]
    public void Lease_GeneraNombresDistintos_ParaElMismoSufijo()
    {
        var primero = TestDatabaseLease.Create("Duplicado");
        var segundo = TestDatabaseLease.Create("Duplicado");

        primero.DatabaseName.Should().NotBe(segundo.DatabaseName,
            "dos arrendamientos nunca comparten base (evita limpiezas cruzadas)");
        primero.SufijoUnico.Should().NotBe(segundo.SufijoUnico);

        foreach (var lease in new[] { primero, segundo })
        {
            TestDatabaseGuard.EsBaseDePruebas(lease.DatabaseName).Should().BeTrue();
            TestDatabaseGuard.EsBasePrincipal(lease.DatabaseName).Should().BeFalse();
            TestDatabaseGuard.EsBaseDelSistema(lease.DatabaseName).Should().BeFalse();
        }
    }

    [Fact]
    public void Lease_ExponeConexionYApuntaAlServidorAutorizado()
    {
        var lease = TestDatabaseLease.Create("Revalidacion");

        var nombre = TestDatabaseGuard.EnsureIsolated(lease.ConnectionString);

        nombre.Should().Be(lease.DatabaseName);
        lease.ConnectionString.Should().Contain(TestDatabaseGuard.AuthorizedServer);
        lease.Options.Should().NotBeNull("las opciones EF se construyen ya validadas");
    }
}
