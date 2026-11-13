using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Services;
using MiCaminoBJJ.Web.Services.Admin;
using MiCaminoBJJ.Web.ViewModels;
using MiCaminoBJJ.Web.Tests.Infrastructure;

namespace MiCaminoBJJ.Web.Tests.Persistence;

/// <summary>
/// Etapa multimedia — integración REAL sobre una base de PRUEBA AISLADA y
/// ÚNICA POR EJECUCIÓN (prefijo <c>MiCaminoBJJ_Web_TFI_2026_Test</c>), jamás
/// sobre la principal. Colección propia de las pruebas administrativas
/// (secuencial, sin interferir con los conteos del catálogo).
///
/// Cubre los requisitos del módulo: alta de enlaces YouTube/Shorts con
/// portada automática, validación de URL/formato/límites en servidor,
/// edición con copia sobre escritura para recursos compartidos, portada única,
/// desvinculación SIN borrar el recurso compartido, auditoría completa,
/// límite de recursos y visibilidad en la ficha pública.
///
/// La autoraía (FK UploadedByUserId) usa un usuario de dominio CREADO EN ESTA
/// BASE AISLADA: no se toca ningún usuario de la base principal.
/// </summary>
[Collection("SqlServerLocalDbAdmin")]
public sealed class AdminTecnicasMediaIntegrationTests
{
    private const string Actor = "tester-admin";

    private readonly AdminAisladoFixture _fixture;

    public AdminTecnicasMediaIntegrationTests(AdminAisladoFixture fixture) => _fixture = fixture;

    private void VerificarDisponibilidad() => _fixture.VerificarDisponibilidad();

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string NombreUnico() => $"TEST MEDIA {Guid.NewGuid():N}";

    /// <summary>VideoId de 11 caracteres alfanuméricos (formato real de YouTube).</summary>
    private static string VideoIdUnico() => $"vid{Guid.NewGuid():N}"[..11];

    private static string UrlWatch(string videoId) => $"https://www.youtube.com/watch?v={videoId}";

    private static TecnicaComando Comando(string nombre, int cinturon, int categoria, int posicion) => new(
        Id: 0,
        Nombre: nombre,
        Descripcion: "Descripción de prueba multimedia.",
        Objetivo: "Objetivo de prueba multimedia.",
        PasosJson: "[\"Paso uno\"]",
        ConsejosJson: "[\"Consejo uno\"]",
        CinturonId: cinturon,
        CategoriaId: categoria,
        PosicionId: posicion);

    private static AdminTecnicasCommandService Tecnicas(WebTfDbContext db) =>
        new(db, new WebAuditService(db, NullLogger<WebAuditService>.Instance));

    private static AdminTecnicasMediaCommandService Media(WebTfDbContext db) =>
        new(db, new WebAuditService(db, NullLogger<WebAuditService>.Instance), new ExternalMediaParser());

    private static AdminTecnicasMediaQueryService MediaQuery(WebTfDbContext db) =>
        new(db, new ExternalMediaParser());

    private async Task<(int Cinturon, int Categoria, int Posicion)> OpcionesAsync(WebTfDbContext db) => (
        await db.Belts.AsNoTracking().OrderBy(b => b.Id).Select(b => b.Id).FirstAsync(),
        await db.TechniqueCategories.AsNoTracking().OrderBy(c => c.Id).Select(c => c.Id).FirstAsync(),
        await db.Positions.AsNoTracking().OrderBy(p => p.Id).Select(p => p.Id).FirstAsync());

    /// <summary>
    /// Usuario de dominio con rol Administrador EN LA BASE AISLADA (autoría
    /// obligatoria de MediaAsset.UploadedByUserId). Idempotente.
    /// </summary>
    private async Task<int> UsuarioAdminAsync(WebTfDbContext db)
    {
        var existente = await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Administrador)
            .OrderBy(u => u.Id)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        if (existente > 0)
            return existente;

        var usuario = new MiCaminoBJJ.Core.Entities.User
        {
            Name = "Admin Media Test",
            Email = $"admin-media-{Guid.NewGuid():N}@test.local",
            PasswordHash = "hash-de-prueba",
            PasswordSalt = "salt-de-prueba",
            BeltRank = "Negro",
            Role = UserRole.Administrador,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(usuario);
        await db.SaveChangesAsync();
        return usuario.Id;
    }

    private async Task<int> CrearTecnicaAsync(WebTfDbContext db, string nombre)
    {
        var (cinturon, categoria, posicion) = await OpcionesAsync(db);
        var resultado = await Tecnicas(db).CrearAsync(Comando(nombre, cinturon, categoria, posicion), Actor);

        resultado.Exito.Should().BeTrue("la creación de prueba debe salir bien: " + string.Join(" | ", resultado.Errores));
        var id = resultado.TecnicaId!.Value;
        _fixture.Registrar(id);
        return id;
    }

    private static async Task<MiCaminoBJJ.Core.Entities.AuditLog?> UltimaAuditoriaAsync(
        WebTfDbContext db, string entityType, int entityId) =>
        await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync();

    // ── Alta ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Agregar_YouTube_CreaRecursoYVinculo_PortadaAutomatica_YAuditoria()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        await UsuarioAdminAsync(db);
        var tecnicaId = await CrearTecnicaAsync(db, NombreUnico());
        var videoId = VideoIdUnico();

        var resultado = await Media(db).AgregarAsync(
            tecnicaId, new MediaComando(UrlWatch(videoId), "Kimura en 30 segundos", "Vista previa"), Actor);

        resultado.Exito.Should().BeTrue(string.Join(" | ", resultado.Errores));

        var asset = await db.MediaAssets.AsNoTracking()
            .Where(a => a.ExternalVideoId == videoId)
            .SingleAsync();

        asset.SourceType.Should().Be(MediaSourceType.ExternalLink);
        asset.MediaType.Should().Be(MediaType.Video);
        asset.ExternalProvider.Should().Be(ExternalMediaProvider.YouTube);
        asset.ExternalUrl.Should().Be(UrlWatch(videoId));
        asset.Title.Should().Be("Kimura en 30 segundos");
        asset.Description.Should().Be("Vista previa");
        asset.IsDeleted.Should().BeFalse("el alta nunca crea recursos borrados");
        asset.UploadedByUserId.Should().BeGreaterThan(0, "la FK de autoría debe resolver un usuario de dominio");

        var link = await db.TechniqueMedia.AsNoTracking()
            .Where(tm => tm.TechniqueId == tecnicaId)
            .SingleAsync();

        link.MediaAssetId.Should().Be(asset.Id);
        link.IsCover.Should().BeTrue("el primer recurso queda como portada automáticamente");
        link.DisplayOrder.Should().Be(1);

        var auditoria = await UltimaAuditoriaAsync(db, nameof(TechniqueMedia), link.Id);
        auditoria.Should().NotBeNull();
        auditoria!.Action.Should().Be(AuditAction.Create);
        auditoria.Result.Should().Be(AuditResult.Success);
        auditoria.Details.Should().Contain($"\"Actor\":\"{Actor}\"");
        auditoria.Details.Should().Contain("\"IsCover\":true");
        auditoria.Details.Should().Contain(videoId);
    }

    [Fact]
    public async Task Agregar_ShortDeYouTube_SeAcepta_YLaSeccionExponeReproductorYMiniatura()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        await UsuarioAdminAsync(db);
        var tecnicaId = await CrearTecnicaAsync(db, NombreUnico());
        var videoId = VideoIdUnico();

        var resultado = await Media(db).AgregarAsync(
            tecnicaId, new MediaComando($"https://youtube.com/shorts/{videoId}", "Short demo", null), Actor);

        resultado.Exito.Should().BeTrue(string.Join(" | ", resultado.Errores));

        // La sección administrativa (query) expone miniatura 16:9 + reproductor.
        var recursos = await MediaQuery(db).ObtenerMultimediaAsync(tecnicaId);
        var recurso = recursos.Should().ContainSingle().Subject;

        recurso.EsVideoIntegrado.Should().BeTrue("Shorts genera reproductor integrado");
        recurso.EmbedUrl.Should().Be($"https://www.youtube.com/embed/{videoId}");
        recurso.MiniaturaUrl.Should().Be($"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg");
        recurso.UrlCanonica.Should().Be(UrlWatch(videoId), "el enlace canónico válida también para Shorts");
        recurso.EsPortada.Should().BeTrue();
        recurso.Proveedor.Should().Be("YouTube");
    }

    [Fact]
    public async Task Agregar_URLInvalida_O_Duplicada_NoEscribeNada()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        await UsuarioAdminAsync(db);
        var tecnicaId = await CrearTecnicaAsync(db, NombreUnico());
        var assetsPrevios = await db.MediaAssets.CountAsync();

        // URL de proveedor no soportado.
        var invalida = await Media(db).AgregarAsync(
            tecnicaId, new MediaComando("https://example.com/video.mp4", "Raro", null), Actor);

        invalida.Exito.Should().BeFalse();
        invalida.Errores.Should().Contain(e => e.Contains("proveedor de video soportado"));

        // URL vacía en alta.
        var vacia = await Media(db).AgregarAsync(tecnicaId, new MediaComando("   ", null, null), Actor);
        vacia.Exito.Should().BeFalse();
        vacia.Errores.Should().Contain(e => e.Contains("obligatoria"));

        (await db.MediaAssets.CountAsync()).Should().Be(assetsPrevios, "ninguna validación fallida debe escribir");

        // Mismo video dos veces en la MISMA técnica → duplicado.
        var videoId = VideoIdUnico();
        var ok = await Media(db).AgregarAsync(tecnicaId, new MediaComando(UrlWatch(videoId), null, null), Actor);
        ok.Exito.Should().BeTrue(string.Join(" | ", ok.Errores));

        var duplicado = await Media(db).AgregarAsync(
            tecnicaId, new MediaComando($"https://youtu.be/{videoId}", null, null), Actor);

        duplicado.Exito.Should().BeFalse("la variante youtu.be del MISMO video es duplicado");
        duplicado.Errores.Should().Contain(e => e.Contains("ya está asociado"));
        (await db.TechniqueMedia.CountAsync(tm => tm.TechniqueId == tecnicaId)).Should().Be(1);
    }

    [Fact]
    public async Task Agregar_SobreElLimiteDeRecursos_EsRechazado()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        await UsuarioAdminAsync(db);
        var tecnicaId = await CrearTecnicaAsync(db, NombreUnico());

        for (var i = 0; i < AdminTecnicasMediaCommandService.MaxRecursosPorTecnica; i++)
        {
            var ok = await Media(db).AgregarAsync(
                tecnicaId, new MediaComando(UrlWatch(VideoIdUnico()), $"Video {i}", null), Actor);
            ok.Exito.Should().BeTrue($"el recurso {i + 1} debe entrar dentro del límite: " + string.Join(" | ", ok.Errores));
        }

        var excedido = await Media(db).AgregarAsync(
            tecnicaId, new MediaComando(UrlWatch(VideoIdUnico()), "El que excede", null), Actor);

        excedido.Exito.Should().BeFalse();
        excedido.Errores.Should().Contain(e => e.Contains("límite"));

        (await db.TechniqueMedia.CountAsync(tm => tm.TechniqueId == tecnicaId))
            .Should().Be(AdminTecnicasMediaCommandService.MaxRecursosPorTecnica);
    }

    [Fact]
    public async Task Agregar_TecnicaInexistente_Es404Logico_SinEscritura()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        await UsuarioAdminAsync(db);
        var assetsPrevios = await db.MediaAssets.CountAsync();

        var resultado = await Media(db).AgregarAsync(
            9999999, new MediaComando(UrlWatch(VideoIdUnico()), null, null), Actor);

        resultado.Ausente.Should().BeTrue("la técnica inexistente debe producir 404");
        (await db.MediaAssets.CountAsync()).Should().Be(assetsPrevios);
    }

    // ── Edición ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Editar_ActualizaTituloDescripcionUrl_YAuditoriaUpdate()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        await UsuarioAdminAsync(db);
        var tecnicaId = await CrearTecnicaAsync(db, NombreUnico());
        var videoOriginal = VideoIdUnico();
        var videoNuevo = VideoIdUnico();

        await Media(db).AgregarAsync(
            tecnicaId, new MediaComando(UrlWatch(videoOriginal), "Titulo viejo", "Descripcion vieja"), Actor);

        var link = await db.TechniqueMedia.AsNoTracking()
            .Where(tm => tm.TechniqueId == tecnicaId)
            .SingleAsync();

        var nuevoTitulo = $"Titulo nuevo {Guid.NewGuid():N}"[..40];
        var resultado = await Media(db).EditarAsync(
            tecnicaId, link.Id, new MediaComando(UrlWatch(videoNuevo), nuevoTitulo, "Descripcion nueva"), Actor);

        resultado.Exito.Should().BeTrue(string.Join(" | ", resultado.Errores));

        var asset = await db.MediaAssets.AsNoTracking().SingleAsync(a => a.Id == link.MediaAssetId);
        asset.Title.Should().Be(nuevoTitulo);
        asset.Description.Should().Be("Descripcion nueva");
        asset.ExternalVideoId.Should().Be(videoNuevo, "la URL cambiada re-parsea el videoId");
        asset.UpdatedAt.Should().NotBeNull("la edición registra UpdatedAt");

        var auditoria = await UltimaAuditoriaAsync(db, nameof(MediaAsset), asset.Id);
        auditoria.Should().NotBeNull();
        auditoria!.Action.Should().Be(AuditAction.Update);
        auditoria.Details.Should().Contain($"\"Actor\":\"{Actor}\"");
        // Títulos sin tildes a propósito: System.Text.Json escapa \uXXXX en Details.
        auditoria.Details.Should().Contain("\"OldTitulo\":\"Titulo viejo\"");
        auditoria.Details.Should().Contain("\"Url\":");
    }

    [Fact]
    public async Task Editar_RecursoCompartido_Clona_YNoAfectaALaOtraTecnica()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        await UsuarioAdminAsync(db);

        var tecnicaA = await CrearTecnicaAsync(db, NombreUnico());
        var tecnicaB = await CrearTecnicaAsync(db, NombreUnico());
        var videoId = VideoIdUnico();

        // El mismo recurso queda compartido por DOS técnicas.
        await Media(db).AgregarAsync(tecnicaA, new MediaComando(UrlWatch(videoId), "Compartido", null), Actor);
        var linkA = await db.TechniqueMedia.AsNoTracking()
            .Where(tm => tm.TechniqueId == tecnicaA).SingleAsync();

        db.TechniqueMedia.Add(new MiCaminoBJJ.Core.Entities.TechniqueMedia
        {
            TechniqueId = tecnicaB,
            MediaAssetId = linkA.MediaAssetId,
            IsCover = true,
            DisplayOrder = 1,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var assetsPrevios = await db.MediaAssets.CountAsync();

        // Editar desde la técnica A: copia sobre escritura.
        var resultado = await Media(db).EditarAsync(
            tecnicaA, linkA.Id, new MediaComando(UrlWatch(videoId), "Solo para A", null), Actor);

        resultado.Exito.Should().BeTrue(string.Join(" | ", resultado.Errores));

        var assetsTrasEditar = await db.MediaAssets.CountAsync();
        assetsTrasEditar.Should().Be(assetsPrevios + 1, "el recurso compartido se clona antes de editar");

        // La técnica A ve el título nuevo en SU asset.
        var linkTrasA = await db.TechniqueMedia
            .Include(tm => tm.MediaAsset)
            .Where(tm => tm.Id == linkA.Id)
            .SingleAsync();
        linkTrasA.MediaAssetId.Should().NotBe(linkA.MediaAssetId, "A apunta a la copia");
        linkTrasA.MediaAsset.Title.Should().Be("Solo para A");

        // La técnica B conserva el original intacto.
        var linkB = await db.TechniqueMedia
            .Include(tm => tm.MediaAsset)
            .Where(tm => tm.TechniqueId == tecnicaB)
            .SingleAsync();
        linkB.MediaAssetId.Should().Be(linkA.MediaAssetId, "B sigue apuntando al original");
        linkB.MediaAsset.Title.Should().Be("Compartido", "B no debe ver mutado su recurso");

        var auditoria = await UltimaAuditoriaAsync(db, nameof(MediaAsset), linkTrasA.MediaAssetId);
        auditoria!.Details.Should().Contain("\"Clonado\":true");
    }

    [Fact]
    public async Task Editar_URLInvalida_O_RecursoAjeno_EsRechazado()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        await UsuarioAdminAsync(db);
        var tecnicaId = await CrearTecnicaAsync(db, NombreUnico());

        await Media(db).AgregarAsync(tecnicaId, new MediaComando(UrlWatch(VideoIdUnico()), "Original", null), Actor);
        var link = await db.TechniqueMedia.AsNoTracking().SingleAsync(tm => tm.TechniqueId == tecnicaId);

        var invalida = await Media(db).EditarAsync(
            tecnicaId, link.Id, new MediaComando("ftp://no.soportada", null, null), Actor);

        invalida.Exito.Should().BeFalse();
        invalida.Errores.Should().Contain(e => e.Contains("proveedor de video soportado"));

        // Recurso que pertenece a OTRA técnica → 404 (no se edita a ciegas).
        var ajeno = await Media(db).EditarAsync(
            tecnicaId + 1, link.Id, new MediaComando(null, "Hackeado", null), Actor);
        ajeno.Ausente.Should().BeTrue();

        var asset = await db.MediaAssets.AsNoTracking().SingleAsync(a => a.Id == link.MediaAssetId);
        asset.Title.Should().Be("Original", "ninguna operación rechazada debe modificar el recurso");
    }

    // ── Portada ────────────────────────────────────────────────────────────

    [Fact]
    public async Task MarcarPortada_DejaUnaUnicaPortadaPorTecnica_YSeAudita()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        await UsuarioAdminAsync(db);
        var tecnicaId = await CrearTecnicaAsync(db, NombreUnico());

        await Media(db).AgregarAsync(tecnicaId, new MediaComando(UrlWatch(VideoIdUnico()), "Primero", null), Actor);
        await Media(db).AgregarAsync(tecnicaId, new MediaComando(UrlWatch(VideoIdUnico()), "Segundo", null), Actor);

        var segundo = await db.TechniqueMedia.AsNoTracking()
            .Where(tm => tm.TechniqueId == tecnicaId)
            .OrderByDescending(tm => tm.Id)
            .FirstAsync();

        var resultado = await Media(db).MarcarPortadaAsync(tecnicaId, segundo.Id, Actor);

        resultado.Exito.Should().BeTrue(string.Join(" | ", resultado.Errores));

        var portadas = await db.TechniqueMedia.AsNoTracking()
            .Where(tm => tm.TechniqueId == tecnicaId && tm.IsCover)
            .ToListAsync();

        portadas.Should().ContainSingle("solo UN recurso puede ser portada")
            .Which.Id.Should().Be(segundo.Id);

        var auditoria = await UltimaAuditoriaAsync(db, nameof(TechniqueMedia), segundo.Id);
        auditoria.Should().NotBeNull();
        auditoria!.Action.Should().Be(AuditAction.Update);
        auditoria.Details.Should().Contain("\"IsCover\":true");

        // Idempotente: marcar la misma portada otra vez no duplica auditorías.
        var previas = await db.AuditLogs.CountAsync(a =>
            a.EntityType == nameof(TechniqueMedia) && a.EntityId == segundo.Id);

        await Media(db).MarcarPortadaAsync(tecnicaId, segundo.Id, Actor);

        var posteriores = await db.AuditLogs.CountAsync(a =>
            a.EntityType == nameof(TechniqueMedia) && a.EntityId == segundo.Id);

        posteriores.Should().Be(previas, "repetir la portada ya estable no debe auditar de nuevo");
    }

    // ── Desvincular ────────────────────────────────────────────────────────

    [Fact]
    public async Task Quitar_DesvinculaPeroConservaElRecursoCompartido_YAuditoriaPurga()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        await UsuarioAdminAsync(db);
        var tecnicaId = await CrearTecnicaAsync(db, NombreUnico());

        await Media(db).AgregarAsync(tecnicaId, new MediaComando(UrlWatch(VideoIdUnico()), "Para quitar", null), Actor);
        var link = await db.TechniqueMedia.AsNoTracking().SingleAsync(tm => tm.TechniqueId == tecnicaId);

        var resultado = await Media(db).DesvincularAsync(tecnicaId, link.Id, Actor);

        resultado.Exito.Should().BeTrue(string.Join(" | ", resultado.Errores));

        (await db.TechniqueMedia.CountAsync(tm => tm.Id == link.Id)).Should().Be(0,
            "la asociación se elimina");

        var asset = await db.MediaAssets.AsNoTracking().SingleAsync(a => a.Id == link.MediaAssetId);
        asset.Should().NotBeNull("el recurso compartido NO se elimina junto con la asociación");
        asset!.IsDeleted.Should().BeFalse("jamás se marca borrado al desvincular");

        var auditoria = await UltimaAuditoriaAsync(db, nameof(TechniqueMedia), link.Id);
        auditoria.Should().NotBeNull();
        auditoria!.Action.Should().Be(AuditAction.Purge);
        auditoria.Details.Should().Contain("\"Desvinculado\":true");
        auditoria.Details.Should().Contain("\"MediaAssetConservado\":true");
    }

    [Fact]
    public async Task Quitar_RecursoInexistente_Es404Logico()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();

        var resultado = await Media(db).DesvincularAsync(7, 999999, Actor);

        resultado.Ausente.Should().BeTrue();
    }

    // ── Ficha pública ──────────────────────────────────────────────────────

    [Fact]
    public async Task RecursoApareceEnLaFichaPublica_CuandoLaTecnicaEstaActiva()
    {
        VerificarDisponibilidad();

        await using var db = _fixture.CreateContext();
        await UsuarioAdminAsync(db);
        var tecnicaId = await CrearTecnicaAsync(db, NombreUnico());
        var videoId = VideoIdUnico();

        await Media(db).AgregarAsync(
            tecnicaId, new MediaComando(UrlWatch(videoId), "Visible en la ficha", null), Actor);

        var publica = new TecnicasSqlServerConsultaService(db);
        var tecnica = await publica.GetByIdAsync(tecnicaId);

        tecnica.Should().NotBeNull();
        tecnica!.IsActive.Should().BeTrue("el alta de técnica nace activa");

        var ficha = TecnicaDetalleViewModel.Desde(tecnica, "/Tecnicas", esFuenteDeDemostracion: false);

        var video = ficha.Videos.Should().ContainSingle(
            "la ficha pública debe mostrar el video recién agregado").Subject;
        video.Titulo.Should().Be("Visible en la ficha");
        video.EmbedUrl.Should().Be($"https://www.youtube.com/embed/{videoId}");
        video.MiniaturaUrl.Should().Contain($"/vi/{videoId}/");
        ficha.TieneVideoIntegrado.Should().BeTrue();
    }
}
