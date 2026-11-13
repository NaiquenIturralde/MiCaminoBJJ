using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Services;
using MiCaminoBJJ.Web.Services.Admin;
using MiCaminoBJJ.Web.Tests.Infrastructure;

namespace MiCaminoBJJ.Web.Tests.Persistence;

/// <summary>
/// Etapa 3 — integración REAL sobre una base de PRUEBA AISLADA y ÚNICA POR
/// EJECUCIÓN (prefijo <c>MiCaminoBJJ_Web_TFI_2026_Test</c>), jamás sobre la principal.
///
/// Reglas de aislamiento:
/// - Cada prueba crea técnicas con NOMBRE ÚNICO ("TEST ADM &lt;guid&gt;") y las
///   registra en el fixture (trazabilidad).
/// - La limpieza es la ELIMINACIÓN COMPLETA de la base del arrendamiento:
///   <see cref="TestDatabaseLease"/> exige que la base no existiera antes y
///   valida la conexión efectiva contra su nombre exacto antes de borrar.
/// - La guarda <see cref="TestDatabaseGuard"/> se aplica antes de escribir.
/// - Colección PROPIA de las pruebas administrativas: no comparte la base con
///   los conteos exactos del catálogo (evita interferencias por paralelismo).
/// </summary>
[Collection("SqlServerLocalDbAdmin")]
public sealed class AdminTecnicasIntegrationTests
{
    private const string Actor = "tester-admin";

    private readonly AdminAisladoFixture _fixture;

    public AdminTecnicasIntegrationTests(AdminAisladoFixture fixture) => _fixture = fixture;

    private void VerificarDisponibilidad() => _fixture.VerificarDisponibilidad();

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string NombreUnico() => $"TEST ADM {Guid.NewGuid():N}";

    private static TecnicaComando Comando(string nombre, int cinturon, int categoria, int posicion) => new(
        Id: 0,
        Nombre: nombre,
        Descripcion: "Descripción de prueba administrativa.",
        Objetivo: "Objetivo de prueba administrativa.",
        PasosJson: "[\"Paso uno\",\"Paso dos\"]",
        ConsejosJson: "[\"Consejo uno\"]",
        CinturonId: cinturon,
        CategoriaId: categoria,
        PosicionId: posicion);

    private static AdminTecnicasCommandService Comandos(WebTfDbContext db) =>
        new(db, new WebAuditService(db, NullLogger<WebAuditService>.Instance));

    private async Task<(int Cinturon, int Categoria, int Posicion)> OpcionesAsync(WebTfDbContext db) => (
        await db.Belts.AsNoTracking().OrderBy(b => b.Id).Select(b => b.Id).FirstAsync(),
        await db.TechniqueCategories.AsNoTracking().OrderBy(c => c.Id).Select(c => c.Id).FirstAsync(),
        await db.Positions.AsNoTracking().OrderBy(p => p.Id).Select(p => p.Id).FirstAsync());

    private async Task<int> CrearAsync(WebTfDbContext db, string nombre)
    {
        var (cinturon, categoria, posicion) = await OpcionesAsync(db);
        var resultado = await Comandos(db).CrearAsync(Comando(nombre, cinturon, categoria, posicion), Actor);

        resultado.Exito.Should().BeTrue("la creación de prueba debe salir bien: " + string.Join(" | ", resultado.Errores));
        var id = resultado.TecnicaId!.Value;
        _fixture.Registrar(id);
        return id;
    }

    /// <summary>Id del cinturón del catálogo por nombre canónico (Blanco…Negro).</summary>
    private static async Task<int> IdCinturonAsync(WebTfDbContext db, string nombreCinturon) =>
        await db.Belts.AsNoTracking()
            .Where(b => b.Name == nombreCinturon)
            .Select(b => b.Id)
            .FirstAsync();

    /// <summary>Crea una técnica ASIGNADA al cinturón indicado (para los filtros).</summary>
    private async Task<int> CrearEnCinturonAsync(WebTfDbContext db, string nombre, string nombreCinturon)
    {
        var (_, categoria, posicion) = await OpcionesAsync(db);
        var cinturon = await IdCinturonAsync(db, nombreCinturon);

        var resultado = await Comandos(db).CrearAsync(Comando(nombre, cinturon, categoria, posicion), Actor);

        resultado.Exito.Should().BeTrue("la creación de prueba debe salir bien: " + string.Join(" | ", resultado.Errores));
        var id = resultado.TecnicaId!.Value;
        _fixture.Registrar(id);
        return id;
    }

    private static async Task<MiCaminoBJJ.Core.Entities.AuditLog?> UltimaAuditoriaAsync(WebTfDbContext db, int tecnicaId) =>
        await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == "Technique" && a.EntityId == tecnicaId)
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync();

    // ── Creación ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Creacion_Valida_Persiste_ConReglasDeDominio_Y_AuditoriaCompleta()
    {
        VerificarDisponibilidad();

        var nombre = NombreUnico();
        await using var db = _fixture.CreateContext();

        var id = await CrearAsync(db, nombre);

        var tecnica = await db.Techniques.AsNoTracking().SingleAsync(t => t.Id == id);
        tecnica.Name.Should().Be(nombre);
        tecnica.NameNormalized.Should().Be(
            MiCaminoBJJ.Core.Services.TextNormalizer.Normalize(nombre),
            "la normalización aplicada al alta debe ser la regla del dominio");
        tecnica.IsActive.Should().BeTrue("el alta siempre nace activa");
        tecnica.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5), "CreatedAt debe ser UTC reciente");
        tecnica.Steps.Should().Contain("Paso uno");

        var auditoria = await UltimaAuditoriaAsync(db, id);
        auditoria.Should().NotBeNull();
        auditoria!.Action.Should().Be(AuditAction.Create);
        auditoria.Result.Should().Be(AuditResult.Success);
        auditoria.UserId.Should().BeNull("la web todavía no tiene usuarios persistidos");
        auditoria.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5), "la auditoría debe quedar con fecha UTC reciente");
        auditoria.Details.Should().Contain($"\"Actor\":\"{Actor}\"", "el actor autenticado queda en Details");
        auditoria.Details.Should().Contain("\"IsActive\":true");
        auditoria.ErrorMessage.Should().BeNull();
    }

    // ── Duplicados ────────────────────────────────────────────────────────

    [Fact]
    public async Task Creacion_IgnoraElIdDelFormulario_YUsaIdentidadDeSqlServer()
    {
        VerificarDisponibilidad();

        var nombre = NombreUnico();
        await using var db = _fixture.CreateContext();
        var (cinturon, categoria, posicion) = await OpcionesAsync(db);

        // Overposting: un Id forjado en el formulario no debe convertirse en
        // la identidad de la fila creada.
        var comando = Comando(nombre, cinturon, categoria, posicion) with { Id = 777777 };
        var resultado = await Comandos(db).CrearAsync(comando, Actor);

        resultado.Exito.Should().BeTrue(string.Join(" | ", resultado.Errores));
        resultado.TecnicaId.Should().NotBe(777777,
            "el Id del formulario jamás debe usarse como identidad en la base");

        _fixture.Registrar(resultado.TecnicaId!.Value);
        var fila = await db.Techniques.AsNoTracking()
            .SingleAsync(t => t.Id == resultado.TecnicaId);
        fila.Name.Should().Be(nombre);
    }

    [Fact]
    public async Task Creacion_Duplicada_PorNormalizacion_EsRechazada_YNoDuplicaFilas()
    {
        VerificarDisponibilidad();

        var nombre = NombreUnico();
        await using var db = _fixture.CreateContext();

        var idOriginal = await CrearAsync(db, nombre);

        // Misma técnica en minúsculas: la regla de TextNormalizer debe colapsarlas en el mismo nombre.
        var (cinturon, categoria, posicion) = await OpcionesAsync(db);
        var duplicado = await Comandos(db).CrearAsync(
            Comando(nombre.ToLowerInvariant(), cinturon, categoria, posicion),
            Actor);

        duplicado.Exito.Should().BeFalse();
        duplicado.Errores.Should().Contain(e => e.Contains("Ya existe"));

        var filas = await db.Techniques.AsNoTracking()
            .Where(t => t.NameNormalized == MiCaminoBJJ.Core.Services.TextNormalizer.Normalize(nombre))
            .CountAsync();
        filas.Should().Be(1, "el rechazo no debe haber insertado nada");
        idOriginal.Should().BeGreaterThan(0);
    }

    // ── Edición ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Edicion_ActualizaCampos_Normalizacion_YAuditoriaUpdate()
    {
        VerificarDisponibilidad();

        var nombre = NombreUnico();
        await using var db = _fixture.CreateContext();
        var id = await CrearAsync(db, nombre);

        var (cinturon, categoria, posicion) = await OpcionesAsync(db);
        var otroCinturon = await db.Belts.AsNoTracking()
            .Where(b => b.Id != cinturon)
            .OrderBy(b => b.Id)
            .Select(b => b.Id)
            .FirstAsync();

        var nuevoNombre = nombre + " v2";
        var comando = Comando(nuevoNombre, otroCinturon, categoria, posicion) with { Id = id };

        var resultado = await Comandos(db).EditarAsync(id, comando, Actor);
        resultado.Exito.Should().BeTrue(string.Join(" | ", resultado.Errores));

        var tecnica = await db.Techniques.AsNoTracking().SingleAsync(t => t.Id == id);
        tecnica.Name.Should().Be(nuevoNombre);
        tecnica.NameNormalized.Should().Be(MiCaminoBJJ.Core.Services.TextNormalizer.Normalize(nuevoNombre));
        tecnica.BeltId.Should().Be(otroCinturon);
        tecnica.UpdatedAt.Should().NotBeNull("la edición registra UpdatedAt");

        var auditoria = await UltimaAuditoriaAsync(db, id);
        auditoria!.Action.Should().Be(AuditAction.Update);
        auditoria.Details.Should().Contain($"\"OldName\":\"{nombre}\"");
        auditoria.Details.Should().Contain($"\"Name\":\"{nuevoNombre}\"");
        auditoria.Details.Should().Contain($"\"Actor\":\"{Actor}\"");
    }

    [Fact]
    public async Task Edicion_ConservandoSuPropioNombre_NoEsDuplicado()
    {
        VerificarDisponibilidad();

        var nombre = NombreUnico();
        await using var db = _fixture.CreateContext();
        var id = await CrearAsync(db, nombre);

        var (cinturon, categoria, posicion) = await OpcionesAsync(db);
        var resultado = await Comandos(db).EditarAsync(
            id, Comando(nombre, cinturon, categoria, posicion) with { Id = id }, Actor);

        resultado.Exito.Should().BeTrue("editar sin cambiar el nombre no debe chocar consigo mismo");
        resultado.Ausente.Should().BeFalse();
    }

    // ── Desactivación / Reactivación + consulta pública ───────────────────

    [Fact]
    public async Task Desactivacion_Y_Reactivacion_AfectanLaBibliotecaPublica_YSeAuditan()
    {
        VerificarDisponibilidad();

        var nombre = NombreUnico();
        await using var db = _fixture.CreateContext();
        var id = await CrearAsync(db, nombre);

        var publica = new TecnicasSqlServerConsultaService(db);

        // Activa → visible en la biblioteca pública.
        (await publica.SearchAsync(new MiCaminoBJJ.Core.Interfaces.TechniqueSearchFilter { SearchTerm = nombre }))
            .Should().ContainSingle(t => t.Name == nombre);

        // Desactivar.
        var desactivar = await Comandos(db).DesactivarAsync(id, Actor);
        desactivar.Exito.Should().BeTrue();

        var trasDesactivar = await db.Techniques.AsNoTracking().SingleAsync(t => t.Id == id);
        trasDesactivar.IsActive.Should().BeFalse("la desactivación es lógica: nunca borra filas");

        (await publica.SearchAsync(new MiCaminoBJJ.Core.Interfaces.TechniqueSearchFilter { SearchTerm = nombre }))
            .Should().BeEmpty("una técnica desactivada no debe listarse en la biblioteca pública");

        // El detalle por Id sigue disponible para el administrador (y el controlador público responde 404 por IsActive).
        (await publica.GetByIdAsync(id)).Should().NotBeNull();

        var auditoriaSoft = await UltimaAuditoriaAsync(db, id);
        auditoriaSoft!.Action.Should().Be(AuditAction.SoftDelete);
        auditoriaSoft.Details.Should().Contain("\"IsActive\":false");

        // Reactivar.
        var reactivar = await Comandos(db).ReactivarAsync(id, Actor);
        reactivar.Exito.Should().BeTrue();

        var trasReactivar = await db.Techniques.AsNoTracking().SingleAsync(t => t.Id == id);
        trasReactivar.IsActive.Should().BeTrue();

        (await publica.SearchAsync(new MiCaminoBJJ.Core.Interfaces.TechniqueSearchFilter { SearchTerm = nombre }))
            .Should().ContainSingle(t => t.Name == nombre);

        var auditoriaRestore = await UltimaAuditoriaAsync(db, id);
        auditoriaRestore!.Action.Should().Be(AuditAction.Restore);
        auditoriaRestore.Details.Should().Contain("\"IsActive\":true");
    }

    // ── Rechazos ──────────────────────────────────────────────────────────

    [Fact]
    public async Task IdentificadoresInexistentes_SonRechazados_SinEscrituras()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        var (cinturon, categoria, posicion) = await OpcionesAsync(db);
        var comandos = Comandos(db);

        var editar = await comandos.EditarAsync(999999, Comando(NombreUnico(), cinturon, categoria, posicion), Actor);
        editar.Ausente.Should().BeTrue();
        editar.Exito.Should().BeFalse();

        var desactivar = await comandos.DesactivarAsync(999999, Actor);
        desactivar.Ausente.Should().BeTrue();

        var reactivar = await comandos.ReactivarAsync(999999, Actor);
        reactivar.Ausente.Should().BeTrue();

        var auditorias = await db.AuditLogs.AsNoTracking()
            .CountAsync(a => a.EntityType == "Technique" && a.EntityId == 999999);
        auditorias.Should().Be(0, "una operación rechazada no debe dejar rastro de auditoría");
    }

    [Fact]
    public async Task ClaveForaneaInvalida_EsRechazada_YNoPersisteNada()
    {
        VerificarDisponibilidad();

        var nombre = NombreUnico();
        await using var db = _fixture.CreateContext();
        var (_, categoria, posicion) = await OpcionesAsync(db);

        var resultado = await Comandos(db).CrearAsync(
            Comando(nombre, cinturon: 999999, categoria, posicion), Actor);

        resultado.Exito.Should().BeFalse();
        resultado.Errores.Should().Contain(e => e.Contains("cinturón") && e.Contains("no existe"));

        (await db.Techniques.AsNoTracking().AnyAsync(t => t.Name == nombre)).Should().BeFalse();

        var auditorias = await db.AuditLogs.AsNoTracking()
            .CountAsync(a => a.EntityType == "Technique" && a.Details != null && a.Details.Contains(nombre));
        auditorias.Should().Be(0);
    }

    // ── Reversión cuando falla la auditoría ───────────────────────────────

    [Fact]
    public async Task AuditoriaQueFalla_RevierteLaTransaccionCompleta()
    {
        VerificarDisponibilidad();

        var nombre = NombreUnico();
        await using var db = _fixture.CreateContext();
        var (cinturon, categoria, posicion) = await OpcionesAsync(db);

        var servicio = new AdminTecnicasCommandService(db, new AuditoriaFalla());

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CrearAsync(Comando(nombre, cinturon, categoria, posicion), Actor));
        excepcion.Message.Should().Contain("auditoría");

        // Transacción revertida: ni la técnica ni la auditoría quedaron.
        await using var dbLimpio = _fixture.CreateContext();
        (await dbLimpio.Techniques.AsNoTracking().AnyAsync(t => t.Name == nombre))
            .Should().BeFalse("si la auditoría falla, la operación no puede quedar a medias");
        (await dbLimpio.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityType == "Technique" && a.Details != null && a.Details.Contains(nombre)))
            .Should().BeFalse();
    }

    // ── Conservación de las consultas públicas ────────────────────────────

    [Fact]
    public async Task ConsultasPublicas_SeConservan_Intactas_DuranteElCrud()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        var publica = new TecnicasSqlServerConsultaService(db);

        // Catálogo sembrado intacto (búsqueda por alias como en la Etapa 2B).
        var porAlias = await publica.SearchAsync(new MiCaminoBJJ.Core.Interfaces.TechniqueSearchFilter { SearchTerm = "RNC" });
        porAlias.Should().ContainSingle(t => t.Name == "Mata león");

        // Las 11 técnicas del catálogo siguen presentes y activas.
        var catalogo = await publica.SearchAsync(new MiCaminoBJJ.Core.Interfaces.TechniqueSearchFilter());
        catalogo.Should().HaveCountGreaterThanOrEqualTo(11);
        catalogo.Should().Contain(t => t.Name == "Escape de montada");
        catalogo.Should().OnlyContain(t => t.IsActive);

        // Opciones de los filtros públicos: 5 cinturones (incluye Negro).
        (await publica.GetBeltsAsync()).Should().HaveCount(5);
        (await publica.GetCategoriesAsync()).Should().HaveCount(5);
        (await publica.GetPositionsAsync()).Should().HaveCount(5);
    }

    // ── Filtros del listado: cinturón, estado y búsqueda ──────────────────

    [Fact]
    public async Task Listado_SoloPorCinturon_ConBusquedaVacia_DevuelveTodasLasDeEseCinturon()
    {
        VerificarDisponibilidad();

        var violeta = NombreUnico();
        var azul = NombreUnico();
        await using var db = _fixture.CreateContext();
        await CrearEnCinturonAsync(db, violeta, "Violeta");
        await CrearEnCinturonAsync(db, azul, "Azul");

        var idVioleta = await IdCinturonAsync(db, "Violeta");
        var servicio = new AdminTecnicasQueryService(db);

        // Buscador vacío + solo cinturón: el tercer filtro debe bastar.
        var resultado = await servicio.ListarAsync(buscar: null, activa: null, cinturon: idVioleta);

        resultado.Should().Contain(t => t.Name == violeta);
        resultado.Should().NotContain(t => t.Name == azul, "el filtro de cinturón es excluyente");
        resultado.Should().OnlyContain(t => t.BeltId == idVioleta,
            "el filtrado debe ocurrir en SQL sobre BeltId");
        resultado.Should().OnlyContain(t => t.Belt!.Name == "Violeta");
    }

    [Fact]
    public async Task Listado_SoloPorEstado_DevuelveSoloLasQueCorresponden()
    {
        VerificarDisponibilidad();

        var nombre = NombreUnico();
        await using var db = _fixture.CreateContext();
        var id = await CrearAsync(db, nombre);

        (await Comandos(db).DesactivarAsync(id, Actor)).Exito.Should()
            .BeTrue("la desactivación de prueba debe salir bien");

        var servicio = new AdminTecnicasQueryService(db);

        var desactivadas = await servicio.ListarAsync(buscar: null, activa: false, cinturon: null);
        desactivadas.Should().Contain(t => t.Name == nombre);

        var activas = await servicio.ListarAsync(buscar: null, activa: true, cinturon: null);
        activas.Should().NotContain(t => t.Name == nombre);
    }

    [Fact]
    public async Task Listado_PorCinturonYEstado_SinBusqueda_IncluyeLasDesactivadasDeEseCinturon()
    {
        VerificarDisponibilidad();

        var desactivadaVioleta = NombreUnico();
        var desactivadaAzul = NombreUnico();
        await using var db = _fixture.CreateContext();

        var idVioletaDesactivada = await CrearEnCinturonAsync(db, desactivadaVioleta, "Violeta");
        var idAzulDesactivada = await CrearEnCinturonAsync(db, desactivadaAzul, "Azul");
        (await Comandos(db).DesactivarAsync(idVioletaDesactivada, Actor)).Exito.Should().BeTrue();
        (await Comandos(db).DesactivarAsync(idAzulDesactivada, Actor)).Exito.Should().BeTrue();

        var idVioleta = await IdCinturonAsync(db, "Violeta");
        var servicio = new AdminTecnicasQueryService(db);

        var resultado = await servicio.ListarAsync(buscar: null, activa: false, cinturon: idVioleta);

        resultado.Should().Contain(t => t.Name == desactivadaVioleta,
            "cinturón + desactivadas, con la búsqueda vacía");
        resultado.Should().NotContain(t => t.Name == desactivadaAzul);
        resultado.Should().OnlyContain(t => t.BeltId == idVioleta);
    }

    [Fact]
    public async Task Listado_CombinaBusquedaCinturonYEstado_SoloDevuelveLoQueCumpleLosTres()
    {
        VerificarDisponibilidad();

        var token = Guid.NewGuid().ToString("N");
        var activaVioleta = $"TEST ADM {token} KIMURA VIOLETA";
        var desactivadaVioleta = $"TEST ADM {token} KIMURA VIOLETA OFF";
        var activaAzul = $"TEST ADM {token} KIMURA AZUL";

        await using var db = _fixture.CreateContext();
        var idActivaVioleta = await CrearEnCinturonAsync(db, activaVioleta, "Violeta");
        var idDesactivadaVioleta = await CrearEnCinturonAsync(db, desactivadaVioleta, "Violeta");
        await CrearEnCinturonAsync(db, activaAzul, "Azul");
        (await Comandos(db).DesactivarAsync(idDesactivadaVioleta, Actor)).Exito.Should().BeTrue();

        var idVioleta = await IdCinturonAsync(db, "Violeta");
        var servicio = new AdminTecnicasQueryService(db);

        var resultado = await servicio.ListarAsync(buscar: token, activa: true, cinturon: idVioleta);

        resultado.Should().ContainSingle(t => t.Id == idActivaVioleta,
            "solo la que cumple nombre + cinturón + estado sobrevive al combinado");
        resultado.Should().NotContain(t => t.Name == desactivadaVioleta, "el estado la descarta");
        resultado.Should().NotContain(t => t.Name == activaAzul, "el cinturón la descarta");
    }

    [Fact]
    public async Task Listado_BusquedaYCinturonSinCoincidencias_DevuelveVacio()
    {
        VerificarDisponibilidad();

        var token = Guid.NewGuid().ToString("N");
        await using var db = _fixture.CreateContext();
        await CrearEnCinturonAsync(db, $"TEST ADM {token} KIMURA", "Azul");

        var idNegro = await IdCinturonAsync(db, "Negro");
        var servicio = new AdminTecnicasQueryService(db);

        var resultado = await servicio.ListarAsync(buscar: token, activa: null, cinturon: idNegro);

        resultado.Should().BeEmpty("la combinación sin coincidencias debe devolver lista vacía");
    }

    [Fact]
    public async Task Listado_Cinturones_CatálogoCompleto_EnElOrdenDelDominio()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        var servicio = new AdminTecnicasQueryService(db);

        var cinturones = await servicio.ObtenerCinturonesAsync();

        cinturones.Select(b => b.Name).Should().Equal("Blanco", "Azul", "Violeta", "Marrón", "Negro");
        cinturones.Should().OnlyContain(b => b.Id > 0, "las opciones salen del catálogo existente");
    }

    // ── Fakes ─────────────────────────────────────────────────────────────

    private sealed class AuditoriaFalla : IAuditService
    {
        public Task RecordAsync(
            int? userId, AuditAction action, string entityType, int entityId,
            AuditResult result = AuditResult.Success,
            IReadOnlyDictionary<string, object?>? changes = null,
            string? errorMessage = null) => Task.CompletedTask;

        public Task RecordStrictAsync(
            int? userId, AuditAction action, string entityType, int entityId,
            AuditResult result = AuditResult.Success,
            IReadOnlyDictionary<string, object?>? changes = null,
            string? errorMessage = null) =>
            throw new InvalidOperationException("Fallo de auditoría simulado.");
    }
}
