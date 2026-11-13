using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Services;
using MiCaminoBJJ.Web.Tests.Infrastructure;

namespace MiCaminoBJJ.Web.Tests.Persistence;

/// <summary>
/// PASO 1.2 — referencias del recorrido de cinturón blanco contra la biblioteca.
///
/// La importación del PDF renombra 4 técnicas (el nombre histórico queda como
/// ALIAS) y oculta las 7 sin video (IsActive = 0). Las fichas del recorrido
/// referencian técnicas por nombre histórico: sin tolerancia a alias y a
/// técnicas ocultas, esos enlaces se perderían.
///
/// Ejecuta sobre la base de PRUEBA AISLADA del catálogo (jamás la principal):
/// crea técnicas/alias PROPIOS con nombres únicos y no toca filas compartidas.
/// </summary>
[Collection("SqlServerLocalDb")]
public sealed class RecorridoReferenciasBibliotecaTests
{
    private readonly CatalogoAisladoFixture _fixture;

    public RecorridoReferenciasBibliotecaTests(CatalogoAisladoFixture fixture) => _fixture = fixture;

    private void VerificarDisponibilidad() => _fixture.VerificarDisponibilidad();

    private static string NombreUnico(string prefijo) => $"TEST {prefijo} {Guid.NewGuid():N}";

    private async Task<int> CrearTecnicaAsync(WebTfDbContext db, string nombre, bool activa)
    {
        var (cinturon, categoria, posicion) = await OpcionesAsync(db);

        var tecnica = new Technique
        {
            Name = nombre,
            NameNormalized = TextNormalizer.Normalize(nombre),
            Description = "Técnica de prueba de referencias del recorrido.",
            Objective = "Verificar la resolución nombre/alias/oculta.",
            BeltId = cinturon,
            CategoryId = categoria,
            PositionId = posicion,
            IsActive = activa,
            CreatedAt = DateTime.UtcNow
        };

        db.Techniques.Add(tecnica);
        await db.SaveChangesAsync();
        _fixture.Registrar(tecnica.Id);
        return tecnica.Id;
    }

    private async Task AgregarAliasAsync(WebTfDbContext db, int tecnicaId, string alias)
    {
        db.TechniqueAliases.Add(new TechniqueAlias
        {
            TechniqueId = tecnicaId,
            Name = alias,
            NameNormalized = TextNormalizer.Normalize(alias),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task<(int Cinturon, int Categoria, int Posicion)> OpcionesAsync(WebTfDbContext db) => (
        await db.Belts.AsNoTracking().OrderBy(b => b.Id).Select(b => b.Id).FirstAsync(),
        await db.TechniqueCategories.AsNoTracking().OrderBy(c => c.Id).Select(c => c.Id).FirstAsync(),
        await db.Positions.AsNoTracking().OrderBy(p => p.Id).Select(p => p.Id).FirstAsync());

    // ── Nombre exacto (comportamiento histórico, debe conservarse) ─────────

    [Fact]
    public async Task NombreExacto_Activa_Resuelve_ConToleranciaATexto()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        var servicio = new TecnicasSqlServerConsultaService(db);

        var encontrada = await servicio.GetByNombreOAliasExactoAsync("escapE dE mOntadA");

        encontrada.Should().NotBeNull("el match exacto con TextNormalizer debe seguir funcionando");
        encontrada!.Name.Should().Be("Escape de montada");
        encontrada.IsActive.Should().BeTrue();
        encontrada.Belt.Should().NotBeNull("la ficha del recorrido muestra el cinturón real");
    }

    [Fact]
    public async Task NombreInexistente_DevuelveNull()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        var servicio = new TecnicasSqlServerConsultaService(db);

        (await servicio.GetByNombreOAliasExactoAsync(NombreUnico("FANTASMA"))).Should().BeNull();
        (await servicio.GetByNombreOAliasExactoAsync("   ")).Should().BeNull("entrada vacía = sin resolución");
        (await servicio.GetByNombreOAliasExactoAsync(null)).Should().BeNull();
    }

    // ── Renombrados por la importación (nombre histórico = alias) ──────────

    [Fact]
    public async Task NombreHistorico_Renombrada_ResuelvePorAlias()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        var servicio = new TecnicasSqlServerConsultaService(db);

        var nombreNuevo = NombreUnico("RENOMBRADA");
        var nombreHistorico = NombreUnico("HISTORICO");
        var tecnicaId = await CrearTecnicaAsync(db, nombreNuevo, activa: true);
        await AgregarAliasAsync(db, tecnicaId, nombreHistorico);

        // La referencia del recorrido usa el nombre HISTÓRICO (pre-importación).
        var resuelta = await servicio.GetByNombreOAliasExactoAsync(nombreHistorico);

        resuelta.Should().NotBeNull("el nombre histórico debe resolver vía ALIAS tras el renombrado");
        resuelta!.Id.Should().Be(tecnicaId);
        resuelta.Name.Should().Be(nombreNuevo, "se muestra el nombre ACTUAL de la técnica");
    }

    [Fact]
    public async Task RenombradaAdemasOculta_Resuelve_PeroSearchAsyncNoLaTrae()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        var servicio = new TecnicasSqlServerConsultaService(db);

        var nombreNuevo = NombreUnico("RENOM_OCULTA");
        var nombreHistorico = NombreUnico("HIST_OCULTA");
        var tecnicaId = await CrearTecnicaAsync(db, nombreNuevo, activa: false);
        await AgregarAliasAsync(db, tecnicaId, nombreHistorico);

        // Contraste: la búsqueda de la biblioteca (solo activas) NO la trae…
        var publicas = await servicio.SearchAsync(new TechniqueSearchFilter { SearchTerm = nombreHistorico });
        publicas.Should().NotContain(t => t.Id == tecnicaId,
            "SearchAsync es la biblioteca pública: solo activas");

        // …pero la resolución del recorrido SÍ (nombre histórico vía alias).
        var resuelta = await servicio.GetByNombreOAliasExactoAsync(nombreHistorico);
        resuelta.Should().NotBeNull("renombrada + oculta debe seguir resolviendo para el recorrido");
        resuelta!.Id.Should().Be(tecnicaId);
        resuelta.IsActive.Should().BeFalse();
        resuelta.Belt.Should().NotBeNull();
    }

    // ── Ocultamiento (las 7 administrativas sin video) ─────────────────────

    [Fact]
    public async Task TecnicaOculta_SinAlias_ResuelveParaElRecorrido()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        var servicio = new TecnicasSqlServerConsultaService(db);

        var nombre = NombreUnico("OCULTA");
        var tecnicaId = await CrearTecnicaAsync(db, nombre, activa: false);

        // Referencia directa por nombre (caso "Puente y giro desde control lateral").
        var publicas = await servicio.SearchAsync(new TechniqueSearchFilter { SearchTerm = nombre });
        publicas.Should().NotContain(t => t.Id == tecnicaId);

        var resuelta = await servicio.GetByNombreOAliasExactoAsync(nombre);

        resuelta.Should().NotBeNull("las técnicas ocultas siguen existiendo: el recorrido debe enlazar su nombre");
        resuelta!.Id.Should().Be(tecnicaId);
        resuelta.IsActive.Should().BeFalse("para que la vista decida NO enlazar a su ficha pública (404)");
        resuelta.Belt.Should().NotBeNull("la ficha muestra el cinturón real de la técnica");
    }

    // ── Sin duplicados: no resuelve otra técnica con nombre parecido ───────

    [Fact]
    public async Task ContenidoDelNombre_NoProduceCoincidenciasParciales()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        var servicio = new TecnicasSqlServerConsultaService(db);

        // "Escape de montada" existe; "Escape de montada con codos" NO (en esta base).
        var exacta = await servicio.GetByNombreOAliasExactoAsync("Escape de montada");
        exacta.Should().NotBeNull();

        // Un nombre que solo CONTIENE parte del término no debe resolver.
        var inexistente = await servicio.GetByNombreOAliasExactoAsync(
            "Escape de montada con codos inexistente xyz");
        inexistente.Should().BeNull("el match es EXACTO tras normalizar, nunca por contains");
    }
}
