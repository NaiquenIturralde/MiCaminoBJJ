using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Infrastructure.Services;
using MiCaminoBJJ.Tests.Infrastructure;

namespace MiCaminoBJJ.Tests.Services;

/// <summary>
/// Etapa 1.2 — Auditoría de la gestión administrativa de recursos multimedia.
/// Cubre: incorporación, eliminación lógica, portada, autorización, un único
/// registro por acción de usuario, reversión ante fallo de auditoría y
/// conservación de lecturas sin auditoría.
/// </summary>
public class MediaServiceAuditTests : IDisposable
{
    private const string SecretHash = "HASH-SECRETO-NO-DEBE-APARECER";
    private const string SecretSalt = "SALT-SECRETO-NO-DEBE-APARECER";

    private readonly SqliteTestDatabase _db;
    private readonly FakeSessionService _session;
    private readonly MediaService _sut;
    private readonly int _adminId;
    private readonly int _practitionerId;
    private readonly int _techniqueId;
    private readonly int _beltId;
    private readonly int _categoryId;
    private readonly int _positionId;

    public MediaServiceAuditTests()
    {
        _db = new SqliteTestDatabase();

        var admin = new User
        {
            Name = "Admin",
            Email = "admin@test.local",
            PasswordHash = SecretHash,
            PasswordSalt = SecretSalt,
            BeltRank = "Negro",
            Role = UserRole.Administrador
        };
        var practitioner = new User
        {
            Name = "Ana",
            Email = "ana@test.local",
            PasswordHash = SecretHash,
            PasswordSalt = SecretSalt,
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

        var technique = new Technique
        {
            Name = "Kimura",
            NameNormalized = "KIMURA",
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
        _techniqueId = technique.Id;

        _session = new FakeSessionService { UserId = _adminId, Role = UserRole.Administrador };

        var audit = new AuditService(_db.Context, NullLogger<AuditService>.Instance);
        var authorization = new AdminAuthorizationService(_session, audit);
        _sut = new MediaService(
            _db.Context,
            NullLogger<MediaService>.Instance,
            audit,
            authorization,
            new FakeMediaStorageService());
    }

    public void Dispose() => _db.Dispose();

    // ── Utilidades ───────────────────────────────────────────────────────

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

    private async Task<bool> IsLinkedAsync(int techniqueId, int mediaAssetId)
    {
        await using var ctx = _db.CreateContext();
        return await ctx.TechniqueMedia
            .AnyAsync(tm => tm.TechniqueId == techniqueId && tm.MediaAssetId == mediaAssetId);
    }

    private int SeedMediaAsset(string fileName = "guardia.jpg", MediaType mediaType = MediaType.Image)
    {
        var media = new MediaAsset
        {
            UploadedByUserId = _adminId,
            MediaType = mediaType,
            SourceType = MediaSourceType.PackagedResource,
            OriginalFileName = fileName,
            Title = fileName,
            SyncStatus = SyncStatus.LocalOnly
        };
        _db.Context.MediaAssets.Add(media);
        _db.Context.SaveChanges();
        return media.Id;
    }

    private int LinkMedia(int mediaAssetId, bool isCover = false, int displayOrder = 1)
    {
        var entry = new TechniqueMedia
        {
            TechniqueId = _techniqueId,
            MediaAssetId = mediaAssetId,
            IsCover = isCover,
            DisplayOrder = displayOrder
        };
        _db.Context.TechniqueMedia.Add(entry);
        _db.Context.SaveChanges();
        return entry.Id;
    }

    // ── Incorporación ────────────────────────────────────────────────────

    [Fact]
    public async Task AddOfficialMediaAsync_WithAdministrator_WritesSingleCreateAudit()
    {
        var mediaAssetId = SeedMediaAsset();

        var entry = await _sut.AddOfficialMediaAsync(_techniqueId, mediaAssetId, isCover: true, displayOrder: 1);

        var audits = await ReadAuditsAsync();
        audits.Should().ContainSingle();

        var log = audits[0];
        log.UserId.Should().Be(_adminId);
        log.Action.Should().Be(AuditAction.Create);
        log.EntityType.Should().Be("MediaAsset");
        log.EntityId.Should().Be(mediaAssetId);
        log.Result.Should().Be(AuditResult.Success);
        log.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(2));
        log.Details.Should().Contain(_techniqueId.ToString()).And.Contain(nameof(MediaType.Image));

        entry.MediaAssetId.Should().Be(mediaAssetId);
        (await IsLinkedAsync(_techniqueId, mediaAssetId)).Should().BeTrue();
    }

    [Fact]
    public async Task AddImageFlow_CreateAndIncorporate_WritesExactlyOneAudit()
    {
        // El panel admin crea el recurso y luego lo incorpora a la técnica:
        // una sola auditoría para la acción "Agregar" (sin duplicados).
        var asset = new MediaAsset
        {
            UploadedByUserId = _adminId,
            MediaType = MediaType.Image,
            SourceType = MediaSourceType.PackagedResource,
            OriginalFileName = "nueva.jpg",
            Title = "nueva.jpg",
            SyncStatus = SyncStatus.LocalOnly
        };

        await _sut.CreateAsync(asset);
        await _sut.AddOfficialMediaAsync(_techniqueId, asset.Id, isCover: false, displayOrder: 1);

        var audits = await ReadAuditsAsync();
        audits.Should().ContainSingle();
        audits[0].Action.Should().Be(AuditAction.Create);
        audits[0].EntityId.Should().Be(asset.Id);
    }

    [Fact]
    public async Task CreateAsync_WithPractitioner_ThrowsAndDoesNotPersistAsset()
    {
        _session.UserId = _practitionerId;
        _session.Role = UserRole.Practicante;
        var asset = new MediaAsset
        {
            UploadedByUserId = _practitionerId,
            MediaType = MediaType.Image,
            SourceType = MediaSourceType.PackagedResource,
            OriginalFileName = "no.jpg",
            Title = "no.jpg",
            SyncStatus = SyncStatus.LocalOnly
        };

        var act = async () => await _sut.CreateAsync(asset);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        await using var ctx = _db.CreateContext();
        (await ctx.MediaAssets.CountAsync()).Should().Be(0);
        (await ctx.AuditLogs.ToListAsync()).Should().ContainSingle(a => a.Action == AuditAction.PermissionDenied);
    }

    // ── Eliminación lógica ───────────────────────────────────────────────

    [Fact]
    public async Task RemoveAction_WritesExactlyOneSoftDeleteAudit()
    {
        var mediaAssetId = SeedMediaAsset();
        LinkMedia(mediaAssetId);

        // Misma secuencia que el panel admin ("Quitar").
        await _sut.RemoveOfficialMediaAsync(_techniqueId, mediaAssetId);
        await _sut.SoftDeleteAsync(mediaAssetId);

        var audits = await ReadAuditsAsync();
        audits.Should().ContainSingle("la acción Quitar no debe generar auditorías duplicadas");

        var log = audits[0];
        log.UserId.Should().Be(_adminId);
        log.Action.Should().Be(AuditAction.SoftDelete);
        log.EntityType.Should().Be("MediaAsset");
        log.EntityId.Should().Be(mediaAssetId);
        log.Result.Should().Be(AuditResult.Success);
        log.Details.Should().Contain("IsDeleted").And.Contain("true");

        (await IsLinkedAsync(_techniqueId, mediaAssetId)).Should().BeFalse();
        (await ReadMediaAsync(mediaAssetId))!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task SoftDeleteAsync_CalledTwice_WritesOnlyOneAuditEntry()
    {
        var mediaAssetId = SeedMediaAsset();
        LinkMedia(mediaAssetId);

        await _sut.RemoveOfficialMediaAsync(_techniqueId, mediaAssetId);
        await _sut.SoftDeleteAsync(mediaAssetId);
        await _sut.SoftDeleteAsync(mediaAssetId);

        (await ReadAuditsAsync()).Should().ContainSingle("la eliminación lógica es idempotente: sin duplicados");
    }

    [Fact]
    public async Task SoftDeleteAsync_WithoutSession_ThrowsAndKeepsAssetActive()
    {
        var mediaAssetId = SeedMediaAsset();
        LinkMedia(mediaAssetId);
        _session.UserId = null;
        _session.Role = null;

        var act = async () => await _sut.SoftDeleteAsync(mediaAssetId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        (await ReadMediaAsync(mediaAssetId))!.IsDeleted.Should().BeFalse();
        (await ReadAuditsAsync()).Should().ContainSingle(a => a.Action == AuditAction.PermissionDenied);
    }

    // ── Portada ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SetCoverAsync_WithAdministrator_WritesSingleUpdateAudit()
    {
        var mediaAssetId = SeedMediaAsset();
        LinkMedia(mediaAssetId);

        await _sut.SetCoverAsync(_techniqueId, mediaAssetId);

        var audits = await ReadAuditsAsync();
        audits.Should().ContainSingle();
        var log = audits[0];
        log.UserId.Should().Be(_adminId);
        log.Action.Should().Be(AuditAction.Update);
        log.EntityType.Should().Be("MediaAsset");
        log.EntityId.Should().Be(mediaAssetId);
        log.Details.Should().Contain("IsCover").And.Contain("true");

        await using var ctx = _db.CreateContext();
        (await ctx.TechniqueMedia.SingleAsync(tm => tm.MediaAssetId == mediaAssetId)).IsCover.Should().BeTrue();
    }

    // ── Rechazo de usuarios sin autorización ─────────────────────────────

    [Fact]
    public async Task AddOfficialMediaAsync_WithPractitioner_ThrowsAndDoesNotLink()
    {
        var mediaAssetId = SeedMediaAsset();
        _session.UserId = _practitionerId;
        _session.Role = UserRole.Practicante;

        var act = async () => await _sut.AddOfficialMediaAsync(_techniqueId, mediaAssetId, false, 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        (await IsLinkedAsync(_techniqueId, mediaAssetId)).Should().BeFalse();

        var audits = await ReadAuditsAsync();
        audits.Should().ContainSingle();
        audits[0].Action.Should().Be(AuditAction.PermissionDenied);
        audits[0].UserId.Should().Be(_practitionerId);
        audits[0].Result.Should().Be(AuditResult.Failure);
    }

    // ── Reversión ante fallo crítico ──────────────────────────────────────

    [Fact]
    public async Task SoftDeleteAsync_WhenAuditWriteFails_RollsBackLogicalDelete()
    {
        var mediaAssetId = SeedMediaAsset();
        LinkMedia(mediaAssetId);
        _db.Execute("DROP TABLE AuditLogs;");

        var act = async () => await _sut.SoftDeleteAsync(mediaAssetId);

        await act.Should().ThrowAsync<Exception>("la auditoría estricta propaga el fallo");
        (await ReadMediaAsync(mediaAssetId))!.IsDeleted.Should().BeFalse(
            "no debe existir una eliminación lógica sin su auditoría");
    }

    [Fact]
    public async Task AddOfficialMediaAsync_WhenAuditWriteFails_RollsBackAssociation()
    {
        var mediaAssetId = SeedMediaAsset();
        _db.Execute("DROP TABLE AuditLogs;");

        var act = async () => await _sut.AddOfficialMediaAsync(_techniqueId, mediaAssetId, false, 1);

        await act.Should().ThrowAsync<Exception>();
        (await IsLinkedAsync(_techniqueId, mediaAssetId)).Should().BeFalse();
    }

    // ── Sin auditorías de lectura ─────────────────────────────────────────

    [Fact]
    public async Task ReadOperations_DoNotWriteAuditEntries()
    {
        var mediaAssetId = SeedMediaAsset();
        LinkMedia(mediaAssetId);

        await _sut.GetByIdAsync(mediaAssetId);
        await _sut.GetOfficialByTechniqueAsync(_techniqueId);
        await _sut.GetOfficialMediaEntriesAsync(_techniqueId);
        await _sut.GetPersonalByTechniqueAsync(_adminId, _techniqueId);

        (await ReadAuditsAsync()).Should().BeEmpty("las lecturas no se auditán");
    }

    // ── Información sensible ──────────────────────────────────────────────

    [Fact]
    public async Task MediaOperations_NeverWriteSecretsToAuditLog()
    {
        var mediaAssetId = SeedMediaAsset();
        await _sut.AddOfficialMediaAsync(_techniqueId, mediaAssetId, true, 1);
        await _sut.SetCoverAsync(_techniqueId, mediaAssetId);
        await _sut.RemoveOfficialMediaAsync(_techniqueId, mediaAssetId);
        await _sut.SoftDeleteAsync(mediaAssetId);

        _session.UserId = _practitionerId;
        _session.Role = UserRole.Practicante;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.AddOfficialMediaAsync(_techniqueId, SeedMediaAsset("otro.jpg"), false, 2));

        var audits = await ReadAuditsAsync();
        audits.Should().NotBeEmpty();

        foreach (var entry in audits)
        {
            var blob = $"{entry.Details} {entry.ErrorMessage}";

            blob.Should().NotContain(SecretHash);
            blob.Should().NotContain(SecretSalt);
            blob.Should().NotContain("PasswordHash");
            blob.Should().NotContain("PasswordSalt");
            blob.Should().NotContain("PersonalNotes");
            blob.Should().NotContain("password");
            blob.Should().NotContain("token");
            blob.Should().NotContain("secret");
        }
    }
}
