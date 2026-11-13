using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Infrastructure.Services;
using MiCaminoBJJ.Tests.Infrastructure;

namespace MiCaminoBJJ.Tests.Services;

/// <summary>
/// Etapa 1.2.1 — Integridad de la gestión administrativa de multimedia.
/// Cubre: autorización antes de almacenar, compensación de archivos ante fallo
/// de base de datos, reversión de operaciones incompletas, ausencia de
/// huérfanos, conservación de archivos compartidos y auditoría única.
/// Usa SQLite en memoria + almacenamiento en memoria: no toca la base real.
/// </summary>
public class MediaIntegrityTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly FakeSessionService _session;
    private readonly FakeMediaStorageService _storage;
    private readonly MediaService _sut;
    private readonly int _adminId;
    private readonly int _practitionerId;
    private readonly int _techniqueId;
    private readonly int _beltId;
    private readonly int _categoryId;
    private readonly int _positionId;

    public MediaIntegrityTests()
    {
        _db = new SqliteTestDatabase();

        var admin = new User
        {
            Name = "Admin",
            Email = "admin@test.local",
            PasswordHash = "HASH-NO-DEBE-APARECER",
            PasswordSalt = "SALT-NO-DEBE-APARECER",
            BeltRank = "Negro",
            Role = UserRole.Administrador
        };
        var practitioner = new User
        {
            Name = "Ana",
            Email = "ana@test.local",
            PasswordHash = "HASH-NO-DEBE-APARECER",
            PasswordSalt = "SALT-NO-DEBE-APARECER",
            BeltRank = "Blanco",
            Role = UserRole.Practicante
        };
        var belt = new Belt { Name = "Blanco", DisplayOrder = 1 };
        var category = new TechniqueCategory { Name = "Sumisión", DisplayOrder = 0 };
        var position = new Position { Name = "Espalda", DisplayOrder = 0 };

        _db.Context.AddRange(admin, practitioner, belt, category, position);
        _db.Context.SaveChanges();

        _adminId = admin.Id;
        _practitionerId = practitioner.Id;
        _beltId = belt.Id;
        _categoryId = category.Id;
        _positionId = position.Id;

        _techniqueId = SeedTechnique("Kimura");

        _session = new FakeSessionService { UserId = _adminId, Role = UserRole.Administrador };
        _storage = new FakeMediaStorageService();

        var audit = new AuditService(_db.Context, NullLogger<AuditService>.Instance);
        var authorization = new AdminAuthorizationService(_session, audit);
        _sut = new MediaService(_db.Context, NullLogger<MediaService>.Instance, audit, authorization, _storage);
    }

    public void Dispose() => _db.Dispose();

    // ── Utilidades ───────────────────────────────────────────────────────

    private static MemoryStream NewStream() =>
        new(Encoding.UTF8.GetBytes("contenido-binario-de-prueba"));

    private int SeedTechnique(string name)
    {
        var technique = new Technique
        {
            Name = name,
            NameNormalized = name.ToUpperInvariant(),
            Description = "descripción",
            Objective = "objetivo",
            BeltId = _beltId,
            CategoryId = _categoryId,
            PositionId = _positionId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Context.Techniques.Add(technique);
        _db.Context.SaveChanges();
        return technique.Id;
    }

    private int SeedMediaAsset(string? localPath)
    {
        var media = new MediaAsset
        {
            UploadedByUserId = _adminId,
            MediaType = MediaType.Image,
            SourceType = MediaSourceType.PackagedResource,
            LocalPath = localPath,
            OriginalFileName = "foto.jpg",
            Title = "foto.jpg",
            SyncStatus = SyncStatus.LocalOnly
        };
        _db.Context.MediaAssets.Add(media);
        _db.Context.SaveChanges();

        if (localPath is not null)
            _storage.AddExistingFile(localPath);

        return media.Id;
    }

    private int LinkMedia(int mediaAssetId, int techniqueId)
    {
        var entry = new TechniqueMedia
        {
            TechniqueId = techniqueId,
            MediaAssetId = mediaAssetId,
            IsCover = false,
            DisplayOrder = 1
        };
        _db.Context.TechniqueMedia.Add(entry);
        _db.Context.SaveChanges();
        return entry.Id;
    }

    private async Task<List<AuditLog>> ReadAuditsAsync()
    {
        await using var ctx = _db.CreateContext();
        return await ctx.AuditLogs.AsNoTracking().OrderBy(a => a.Id).ToListAsync();
    }

    private async Task<MediaAsset?> ReadMediaAsync(int mediaAssetId)
    {
        await using var ctx = _db.CreateContext();
        return await ctx.MediaAssets.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mediaAssetId);
    }

    private async Task<int> CountMediaAsync()
    {
        await using var ctx = _db.CreateContext();
        return await ctx.MediaAssets.CountAsync();
    }

    private async Task<int> CountLinksAsync()
    {
        await using var ctx = _db.CreateContext();
        return await ctx.TechniqueMedia.CountAsync();
    }

    private async Task<bool> IsLinkedAsync(int techniqueId, int mediaAssetId)
    {
        await using var ctx = _db.CreateContext();
        return await ctx.TechniqueMedia
            .AnyAsync(tm => tm.TechniqueId == techniqueId && tm.MediaAssetId == mediaAssetId);
    }

    // ── Permisos rechazados ANTES del almacenamiento ──────────────────────

    [Fact]
    public async Task AddFromFile_WithoutSession_ThrowsBeforeWritingAnyFile()
    {
        _session.UserId = null;
        _session.Role = null;

        var act = async () => await _sut.AddOfficialMediaFromFileAsync(
            _techniqueId, NewStream(), "foto.jpg", MediaType.Image, false, 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        _storage.SaveCalls.Should().Be(0, "sin sesión no debe tocarse el disco");
        _storage.Files.Should().BeEmpty();
        (await CountMediaAsync()).Should().Be(0);
        (await CountLinksAsync()).Should().Be(0);

        var audits = await ReadAuditsAsync();
        audits.Should().ContainSingle();
        audits[0].Action.Should().Be(AuditAction.PermissionDenied);
        audits[0].UserId.Should().BeNull();
    }

    [Fact]
    public async Task AddFromFile_WithPractitioner_ThrowsBeforeWritingAnyFile()
    {
        _session.UserId = _practitionerId;
        _session.Role = UserRole.Practicante;

        var act = async () => await _sut.AddOfficialMediaFromFileAsync(
            _techniqueId, NewStream(), "foto.jpg", MediaType.Image, false, 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        _storage.SaveCalls.Should().Be(0, "el permiso se verifica antes de guardar el archivo");
        _storage.Files.Should().BeEmpty();
        (await CountMediaAsync()).Should().Be(0);
        (await CountLinksAsync()).Should().Be(0);

        var audits = await ReadAuditsAsync();
        audits.Should().ContainSingle();
        audits[0].Action.Should().Be(AuditAction.PermissionDenied);
        audits[0].UserId.Should().Be(_practitionerId);
        audits[0].Result.Should().Be(AuditResult.Failure);
    }

    // ── Alta atómica con auditoría única ─────────────────────────────────

    [Fact]
    public async Task AddFromFile_Success_WritesFileAssetLinkAndSingleAudit()
    {
        var entry = await _sut.AddOfficialMediaFromFileAsync(
            _techniqueId, NewStream(), "foto.jpg", MediaType.Image, isCover: true, displayOrder: 1);

        _storage.Files.Should().ContainSingle();
        (await CountMediaAsync()).Should().Be(1);
        (await IsLinkedAsync(_techniqueId, entry.MediaAssetId)).Should().BeTrue();

        var asset = await ReadMediaAsync(entry.MediaAssetId);
        asset!.LocalPath.Should().Be(_storage.Files.Keys.Single());
        asset.UploadedByUserId.Should().Be(_adminId);
        asset.IsDeleted.Should().BeFalse();

        var audits = await ReadAuditsAsync();
        audits.Should().ContainSingle();
        audits[0].UserId.Should().Be(_adminId);
        audits[0].Action.Should().Be(AuditAction.Create);
        audits[0].EntityType.Should().Be("MediaAsset");
        audits[0].EntityId.Should().Be(entry.MediaAssetId);
        audits[0].Result.Should().Be(AuditResult.Success);
        audits[0].Details.Should().Contain(_techniqueId.ToString());
    }

    [Fact]
    public async Task AddFromAsset_Success_CreatesAssetLinksAndWritesSingleAudit()
    {
        var asset = new MediaAsset
        {
            UploadedByUserId = 9999, // debe imponerse el administrador autorizado
            MediaType = MediaType.Video,
            SourceType = MediaSourceType.ExternalLink,
            ExternalUrl = "https://youtube.com/watch?v=abc",
            ExternalProvider = ExternalMediaProvider.YouTube,
            ExternalVideoId = "abc",
            Title = "Video de YouTube",
            SyncStatus = SyncStatus.LocalOnly
        };

        var entry = await _sut.AddOfficialMediaFromAssetAsync(asset, _techniqueId, false, 2);

        (await IsLinkedAsync(_techniqueId, entry.MediaAssetId)).Should().BeTrue();
        (await ReadMediaAsync(entry.MediaAssetId))!.UploadedByUserId.Should().Be(_adminId);

        var audits = await ReadAuditsAsync();
        audits.Should().ContainSingle();
        audits[0].Action.Should().Be(AuditAction.Create);
        audits[0].EntityId.Should().Be(entry.MediaAssetId);
        audits[0].Details.Should().Contain(nameof(MediaSourceType.ExternalLink));
    }

    [Fact]
    public async Task AddFromAsset_WithPractitioner_ThrowsAndCreatesNothing()
    {
        _session.UserId = _practitionerId;
        _session.Role = UserRole.Practicante;
        var asset = new MediaAsset
        {
            UploadedByUserId = _practitionerId,
            MediaType = MediaType.Video,
            SourceType = MediaSourceType.ExternalLink,
            Title = "no debe persistirse",
            SyncStatus = SyncStatus.LocalOnly
        };

        var act = async () => await _sut.AddOfficialMediaFromAssetAsync(asset, _techniqueId, false, 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await CountMediaAsync()).Should().Be(0);
        (await CountLinksAsync()).Should().Be(0);
        (await ReadAuditsAsync()).Should().ContainSingle(a => a.Action == AuditAction.PermissionDenied);
    }

    // ── Fallo de base de datos después de guardar el archivo ─────────────

    [Fact]
    public async Task AddFromFile_WhenDatabaseFailsAfterSavingFile_CompensatesAndLeavesNothing()
    {
        _db.Execute("DROP TABLE AuditLogs;");

        var act = async () => await _sut.AddOfficialMediaFromFileAsync(
            _techniqueId, NewStream(), "foto.jpg", MediaType.Image, false, 1);

        await act.Should().ThrowAsync<Exception>("la auditoría estricta propaga el fallo de la base");

        _storage.SaveCalls.Should().Be(1, "el archivo llegó a guardarse antes del fallo");
        _storage.Files.Should().BeEmpty("el archivo debe compensarse: sin huérfanos en disco");
        (await CountMediaAsync()).Should().Be(0, "el recurso debe revertirse junto con la auditoría");
        (await CountLinksAsync()).Should().Be(0, "no debe quedar ningún vínculo parcial");
    }

    // ── Baja atómica: reversión y archivos compartidos ────────────────────

    [Fact]
    public async Task Remove_WhenDatabaseFails_KeepsLinkAssetAndFile()
    {
        const string path = "media/official/techniques/existente.jpg";
        var assetId = SeedMediaAsset(path);
        LinkMedia(assetId, _techniqueId);
        _db.Execute("DROP TABLE AuditLogs;");

        var act = async () => await _sut.RemoveOfficialMediaAsync(_techniqueId, assetId);

        await act.Should().ThrowAsync<Exception>();

        (await IsLinkedAsync(_techniqueId, assetId)).Should().BeTrue("sin commit no se desvincula");
        (await ReadMediaAsync(assetId))!.IsDeleted.Should().BeFalse("sin commit no hay eliminación lógica");
        _storage.Files.Should().ContainKey(path, "el archivo no puede borrarse si la operación revirtió");
    }

    [Fact]
    public async Task Remove_LastReference_DeletesFilePhysicallyAndWritesSingleAudit()
    {
        const string path = "media/official/techniques/unico.jpg";
        var assetId = SeedMediaAsset(path);
        LinkMedia(assetId, _techniqueId);

        await _sut.RemoveOfficialMediaAsync(_techniqueId, assetId);

        (await IsLinkedAsync(_techniqueId, assetId)).Should().BeFalse();
        (await ReadMediaAsync(assetId))!.IsDeleted.Should().BeTrue();
        _storage.Files.Should().BeEmpty("última referencia: el archivo deja de necesitarse");

        var audits = await ReadAuditsAsync();
        audits.Should().ContainSingle("una acción de 'Quitar' = una auditoría");
        audits[0].Action.Should().Be(AuditAction.SoftDelete);
        audits[0].EntityType.Should().Be("MediaAsset");
        audits[0].EntityId.Should().Be(assetId);
        audits[0].Details.Should().Contain("IsDeleted").And.Contain("true");
    }

    [Fact]
    public async Task Remove_SharedWithAnotherTechnique_KeepsAssetAndFile()
    {
        const string path = "media/official/techniques/compartido.jpg";
        var otherTechniqueId = SeedTechnique("Otra técnica");
        var assetId = SeedMediaAsset(path);
        LinkMedia(assetId, _techniqueId);
        LinkMedia(assetId, otherTechniqueId);

        await _sut.RemoveOfficialMediaAsync(_techniqueId, assetId);

        (await IsLinkedAsync(_techniqueId, assetId)).Should().BeFalse();
        (await IsLinkedAsync(otherTechniqueId, assetId)).Should().BeTrue("la otra técnica sigue usándolo");
        (await ReadMediaAsync(assetId))!.IsDeleted.Should().BeFalse("no se da de baja un recurso compartido");
        _storage.Files.Should().ContainKey(path, "nunca se borra físicamente un archivo con referencias válidas");

        var audits = await ReadAuditsAsync();
        audits.Should().ContainSingle();
        audits[0].Action.Should().Be(AuditAction.Update);
        audits[0].Details.Should().Contain("RemainingReferences").And.Contain("IsDeleted");
    }

    [Fact]
    public async Task Remove_ReferencedByPersonalResource_KeepsAssetAndFile()
    {
        const string path = "media/official/techniques/personal.jpg";
        var assetId = SeedMediaAsset(path);
        LinkMedia(assetId, _techniqueId);
        _db.Context.UserTechniqueResources.Add(new UserTechniqueResource
        {
            UserId = _practitionerId,
            TechniqueId = _techniqueId,
            MediaAssetId = assetId,
            PersonalNotes = "mis apuntes",
            DisplayOrder = 1
        });
        await _db.Context.SaveChangesAsync();

        await _sut.RemoveOfficialMediaAsync(_techniqueId, assetId);

        (await ReadMediaAsync(assetId))!.IsDeleted.Should().BeFalse("el recurso personal sigue usándolo");
        _storage.Files.Should().ContainKey(path);

        var audits = await ReadAuditsAsync();
        audits.Should().ContainSingle();
        audits[0].Action.Should().Be(AuditAction.Update);
    }

    [Fact]
    public async Task Remove_FileSharedByAnotherAssetRow_KeepsPhysicalFile()
    {
        const string path = "media/official/techniques/mismo-archivo.jpg";
        var assetId = SeedMediaAsset(path);
        _db.Context.MediaAssets.Add(new MediaAsset
        {
            UploadedByUserId = _adminId,
            MediaType = MediaType.Image,
            SourceType = MediaSourceType.PackagedResource,
            LocalPath = path, // misma ruta que otro recurso
            OriginalFileName = "copia.jpg",
            Title = "copia.jpg",
            SyncStatus = SyncStatus.LocalOnly
        });
        _db.Context.SaveChanges();
        LinkMedia(assetId, _techniqueId);

        await _sut.RemoveOfficialMediaAsync(_techniqueId, assetId);

        (await ReadMediaAsync(assetId))!.IsDeleted.Should().BeTrue();
        _storage.Files.Should().ContainKey(path, "otro recurso apunta a la misma ruta");

        (await ReadAuditsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Remove_CalledTwice_WritesOnlyOneAuditAndDeletesOnce()
    {
        const string path = "media/official/techniques/una-vez.jpg";
        var assetId = SeedMediaAsset(path);
        LinkMedia(assetId, _techniqueId);

        await _sut.RemoveOfficialMediaAsync(_techniqueId, assetId);
        await _sut.RemoveOfficialMediaAsync(_techniqueId, assetId);

        (await ReadAuditsAsync()).Should().ContainSingle("la segunda acción no cambia nada: sin duplicados");
        _storage.DeleteCalls.Should().Be(1, "el archivo solo se intenta borrar una vez");
    }

    [Fact]
    public async Task Remove_ExternalLinkWithoutFile_WritesSingleAuditWithoutFileOperations()
    {
        var asset = new MediaAsset
        {
            UploadedByUserId = _adminId,
            MediaType = MediaType.Video,
            SourceType = MediaSourceType.ExternalLink,
            ExternalUrl = "https://vimeo.com/123",
            ExternalProvider = ExternalMediaProvider.Vimeo,
            Title = "Video de Vimeo",
            SyncStatus = SyncStatus.LocalOnly
        };
        _db.Context.MediaAssets.Add(asset);
        _db.Context.SaveChanges();
        LinkMedia(asset.Id, _techniqueId);

        await _sut.RemoveOfficialMediaAsync(_techniqueId, asset.Id);

        (await IsLinkedAsync(_techniqueId, asset.Id)).Should().BeFalse();
        (await ReadMediaAsync(asset.Id))!.IsDeleted.Should().BeTrue();
        _storage.DeleteCalls.Should().Be(0, "los enlaces externos no tienen archivo físico");

        (await ReadAuditsAsync()).Should().ContainSingle();
    }

    // ── SoftDelete coherente con los archivos ─────────────────────────────

    [Fact]
    public async Task SoftDelete_Unreferenced_RemovesFileAndWritesSingleAudit()
    {
        const string path = "media/official/techniques/sin-referencias.jpg";
        var assetId = SeedMediaAsset(path); // sin vínculos

        await _sut.SoftDeleteAsync(assetId);

        (await ReadMediaAsync(assetId))!.IsDeleted.Should().BeTrue();
        _storage.Files.Should().BeEmpty();
        (await ReadAuditsAsync()).Should().ContainSingle(a => a.Action == AuditAction.SoftDelete);
    }

    [Fact]
    public async Task SoftDelete_StillReferenced_KeepsPhysicalFile()
    {
        const string path = "media/official/techniques/en-uso.jpg";
        var assetId = SeedMediaAsset(path);
        LinkMedia(assetId, _techniqueId);

        await _sut.SoftDeleteAsync(assetId);

        (await ReadMediaAsync(assetId))!.IsDeleted.Should().BeTrue();
        _storage.Files.Should().ContainKey(path, "el archivo sigue referenciado por un vínculo");
        (await ReadAuditsAsync()).Should().ContainSingle(a => a.Action == AuditAction.SoftDelete);
    }
}
