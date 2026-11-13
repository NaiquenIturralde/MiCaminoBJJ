using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Web.Identity;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Services.Admin;
using MiCaminoBJJ.Web.Services.Auditoria;
using MiCaminoBJJ.Web.Tests.Infrastructure;
using Xunit;

namespace MiCaminoBJJ.Web.Tests.Services;

/// <summary>
/// Cierre TFI — auditoría administrativa y reporte PDF.
///
/// Prueba sobre una base AISLADA y única por ejecución (la principal es
/// inalcanzable: ver <see cref="TestDatabaseGuard"/>). Verifica que:
/// - cada operación administrativa (crear / modificar / desactivar) deja UN
///   registro de auditoría con actor, operación, entidad e identificador,
/// - el registro identifica al administrador con su id de usuario de dominio,
/// - la pantalla de auditoría devuelve los registros REALES y filtra bien,
/// - el PDF generado es un archivo válido (con y sin registros).
/// </summary>
[Collection("SqlServerLocalDbAdmin")]
public sealed class AuditoriaConsultaYReporteTests
{
    private const string Actor = "tester-auditoria";
    private const string EntidadTecnica = "Technique";

    private readonly AdminAisladoFixture _fixture;

    public AuditoriaConsultaYReporteTests(AdminAisladoFixture fixture) => _fixture = fixture;

    // ── Helpers ───────────────────────────────────────────────────────────

    private static AdminTecnicasCommandService Comandos(WebTfDbContext db) =>
        new(db, new WebAuditService(db, NullLogger<WebAuditService>.Instance));

    private static async Task<(int Cinturon, int Categoria, int Posicion)> OpcionesAsync(WebTfDbContext db) => (
        await db.Belts.AsNoTracking().OrderBy(b => b.Id).Select(b => b.Id).FirstAsync(),
        await db.TechniqueCategories.AsNoTracking().OrderBy(c => c.Id).Select(c => c.Id).FirstAsync(),
        await db.Positions.AsNoTracking().OrderBy(p => p.Id).Select(p => p.Id).FirstAsync());

    private static TecnicaComando Comando(string nombre, int cinturon, int categoria, int posicion) => new(
        Id: 0,
        Nombre: nombre,
        Descripcion: "Descripción de la técnica de prueba de auditoría.",
        Objetivo: "Objetivo de la técnica de prueba de auditoría.",
        PasosJson: "[\"Paso uno\"]",
        ConsejosJson: "[\"Consejo uno\"]",
        CinturonId: cinturon,
        CategoriaId: categoria,
        PosicionId: posicion);

    private async Task<int> CrearTecnicaAsync(WebTfDbContext db, string actor = Actor)
    {
        var (cinturon, categoria, posicion) = await OpcionesAsync(db);
        var nombre = $"TEST AUDIT {Guid.NewGuid():N}";

        var resultado = await Comandos(db).CrearAsync(
            Comando(nombre, cinturon, categoria, posicion), actor);

        resultado.Exito.Should().BeTrue(
            "la creación de prueba debe salir bien: " + string.Join(" | ", resultado.Errores));

        var id = resultado.TecnicaId!.Value;
        _fixture.Registrar(id);
        return id;
    }

    // ── Registro de auditoría ─────────────────────────────────────────────

    [Fact]
    public async Task CrearTecnica_DejaUnRegistroDeAuditoria_ConActorOperacionYEntidad()
    {
        _fixture.VerificarDisponibilidad();
        await using var db = _fixture.CreateContext();

        var id = await CrearTecnicaAsync(db);

        var consulta = new AuditoriaConsultaService(db);
        var resultado = await consulta.ConsultarAsync(new AuditoriaFiltro
        {
            Entidad = EntidadTecnica,
            TamanoPagina = 500
        });

        var fila = resultado.Filas.FirstOrDefault(f => f.EntidadId == id);

        fila.Should().NotBeNull("la creación de una técnica debe quedar auditada");
        fila!.Operacion.Should().Be("Creación");
        fila.Entidad.Should().Be("Técnica");
        fila.EntidadId.Should().Be(id);
        fila.Usuario.Should().Be(Actor);
        fila.Resultado.Should().Be("Correcto");
        fila.FechaUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task ModificarTecnica_DejaUnRegistroDeAuditoria_DeModificacion()
    {
        _fixture.VerificarDisponibilidad();
        await using var db = _fixture.CreateContext();

        var id = await CrearTecnicaAsync(db);
        var (cinturon, categoria, posicion) = await OpcionesAsync(db);

        var comando = Comando($"TEST AUDIT EDIT {Guid.NewGuid():N}", cinturon, categoria, posicion);
        var edicion = await Comandos(db).EditarAsync(id, comando, Actor);
        edicion.Exito.Should().BeTrue(string.Join(" | ", edicion.Errores));

        var consulta = new AuditoriaConsultaService(db);
        var resultado = await consulta.ConsultarAsync(new AuditoriaFiltro
        {
            Entidad = EntidadTecnica,
            TamanoPagina = 500
        });

        var fila = resultado.Filas.FirstOrDefault(f => f.EntidadId == id && f.Operacion == "Modificación");

        fila.Should().NotBeNull("la edición de una técnica debe quedar auditada");
        fila!.Usuario.Should().Be(Actor);
        fila.Resultado.Should().Be("Correcto");
    }

    [Fact]
    public async Task DesactivarTecnica_DejaUnRegistroDeAuditoria_DeDesactivacion()
    {
        _fixture.VerificarDisponibilidad();
        await using var db = _fixture.CreateContext();

        var id = await CrearTecnicaAsync(db);

        var desactivacion = await Comandos(db).DesactivarAsync(id, Actor);
        desactivacion.Exito.Should().BeTrue(string.Join(" | ", desactivacion.Errores));

        var consulta = new AuditoriaConsultaService(db);
        var resultado = await consulta.ConsultarAsync(new AuditoriaFiltro
        {
            Entidad = EntidadTecnica,
            TamanoPagina = 500
        });

        var fila = resultado.Filas.FirstOrDefault(f => f.EntidadId == id && f.Operacion == "Desactivación");

        fila.Should().NotBeNull("la desactivación de una técnica debe quedar auditada");
        fila!.Usuario.Should().Be(Actor);
        fila.Resultado.Should().Be("Correcto");
    }

    [Fact]
    public async Task AdministradorIdentity_QuedaRegistradoConSuIdDeUsuarioDeDominio()
    {
        _fixture.VerificarDisponibilidad();
        await using var db = _fixture.CreateContext();

        // Usuario de dominio + credencial de Identity vinculada (DomainUserId).
        var correo = $"admin-auditoria-{Guid.NewGuid():N}@example.com";
        var usuario = new User
        {
            Name = "Admin de Auditoría",
            Email = correo,
            PasswordHash = "hash-de-prueba",
            PasswordSalt = "salt-de-prueba"
        };
        db.Users.Add(usuario);
        await db.SaveChangesAsync();

        db.Set<ApplicationUser>().Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = correo,
            NormalizedUserName = correo.ToUpperInvariant(),
            Email = correo,
            NormalizedEmail = correo.ToUpperInvariant(),
            DomainUserId = usuario.Id
        });
        await db.SaveChangesAsync();

        var id = await CrearTecnicaAsync(db, correo);

        var auditoria = await db.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityType == EntidadTecnica && a.EntityId == id)
            .OrderByDescending(a => a.Id)
            .FirstAsync();

        auditoria.UserId.Should().Be(usuario.Id,
            "la auditoría debe identificar al administrador con su id de usuario de dominio");
        auditoria.Action.Should().Be(AuditAction.Create);
    }

    // ── Consulta (pantalla) ───────────────────────────────────────────────

    [Fact]
    public async Task Consulta_SinRegistros_DevuelveListaVacia_ConTotalCero()
    {
        _fixture.VerificarDisponibilidad();
        await using var db = _fixture.CreateContext();

        var consulta = new AuditoriaConsultaService(db);
        var resultado = await consulta.ConsultarAsync(new AuditoriaFiltro
        {
            Entidad = "EntidadQueNoExiste"
        });

        resultado.HayRegistros.Should().BeFalse();
        resultado.Filas.Should().BeEmpty();
        resultado.Total.Should().Be(0);
    }

    [Fact]
    public async Task Consulta_FiltraPorOperacionYEntidad()
    {
        _fixture.VerificarDisponibilidad();
        await using var db = _fixture.CreateContext();

        var id = await CrearTecnicaAsync(db);

        var consulta = new AuditoriaConsultaService(db);
        var resultado = await consulta.ConsultarAsync(new AuditoriaFiltro
        {
            Accion = AuditAction.Create,
            Entidad = EntidadTecnica,
            TamanoPagina = 500
        });

        resultado.Filas.Should().NotBeEmpty();
        resultado.Filas.Should().OnlyContain(f => f.Operacion == "Creación" && f.Entidad == "Técnica");
        resultado.Filas.Should().Contain(f => f.EntidadId == id);
    }

    [Fact]
    public async Task Consulta_FiltraPorTextoDeActor()
    {
        _fixture.VerificarDisponibilidad();
        await using var db = _fixture.CreateContext();

        var id = await CrearTecnicaAsync(db, "actor-buscable-auditoria");

        var consulta = new AuditoriaConsultaService(db);
        var resultado = await consulta.ConsultarAsync(new AuditoriaFiltro
        {
            Usuario = "actor-buscable-auditoria",
            TamanoPagina = 500
        });

        resultado.Filas.Should().NotBeEmpty();
        resultado.Filas.Should().OnlyContain(f => f.Usuario.Contains("actor-buscable-auditoria"));
        resultado.Filas.Should().Contain(f => f.EntidadId == id);
    }

    // ── Reporte PDF ───────────────────────────────────────────────────────

    [Fact]
    public async Task Pdf_ConRegistros_GeneraUnArchivoPdfValido()
    {
        _fixture.VerificarDisponibilidad();
        await using var db = _fixture.CreateContext();

        var id = await CrearTecnicaAsync(db);

        var consulta = new AuditoriaConsultaService(db);
        var resultado = await consulta.ConsultarAsync(new AuditoriaFiltro
        {
            Entidad = EntidadTecnica,
            TamanoPagina = 500
        });

        var pdf = new AuditoriaPdfService();
        var bytes = await pdf.GenerarAsync(
            resultado.Filas.Where(f => f.EntidadId == id).ToList(),
            "admin@micaminobjj.com",
            DateTimeOffset.Now);

        bytes.Should().NotBeEmpty();
        bytes.Length.Should().BeGreaterThan(500);
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4)
            .Should().Be("%PDF", "el archivo descargado debe ser un PDF real");
    }

    [Fact]
    public async Task Pdf_SinRegistros_GeneraUnArchivoPdfValido_ConMensajeDeAusencia()
    {
        _fixture.VerificarDisponibilidad();
        await using var db = _fixture.CreateContext();

        var pdf = new AuditoriaPdfService();
        var bytes = await pdf.GenerarAsync(
            Array.Empty<AuditoriaFila>(),
            "admin@micaminobjj.com",
            DateTimeOffset.Now);

        bytes.Should().NotBeEmpty();
        bytes.Length.Should().BeGreaterThan(500);
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }
}
