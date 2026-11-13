using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Services;
using MiCaminoBJJ.Tests.Infrastructure;

namespace MiCaminoBJJ.Tests.Services;

/// <summary>
/// Etapa 1.2 — Integración de auditoría en operaciones administrativas.
/// Cubre: registro correcto (actor, acción, entidad, id, UTC, resultado),
/// rechazo de usuarios no autorizados, ausencia de secretos, ausencia de
/// duplicados, reversión ante fallo de auditoría y conservación de lecturas
/// sin cambios de comportamiento.
/// </summary>
public class TechniqueServiceAuditTests : IDisposable
{
    private const string SecretHash = "HASH-SECRETO-NO-DEBE-APARECER";
    private const string SecretSalt = "SALT-SECRETO-NO-DEBE-APARECER";

    private readonly SqliteTestDatabase _db;
    private readonly FakeSessionService _session;
    private readonly TechniqueService _sut;
    private readonly int _adminId;
    private readonly int _practitionerId;
    private readonly int _beltId;
    private readonly int _categoryId;
    private readonly int _positionId;

    public TechniqueServiceAuditTests()
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

        _session = new FakeSessionService { UserId = _adminId, Role = UserRole.Administrador };

        var audit = new AuditService(_db.Context, NullLogger<AuditService>.Instance);
        var authorization = new AdminAuthorizationService(_session, audit);
        _sut = new TechniqueService(_db.Context, audit, authorization);
    }

    public void Dispose() => _db.Dispose();

    // ── Utilidades ───────────────────────────────────────────────────────

    private async Task<List<AuditLog>> ReadAuditsAsync()
    {
        await using var ctx = _db.CreateContext();
        return await ctx.AuditLogs.AsNoTracking().OrderBy(a => a.Id).ToListAsync();
    }

    private async Task<Technique?> ReadTechniqueAsync(int techniqueId)
    {
        await using var ctx = _db.CreateContext();
        return await ctx.Techniques.AsNoTracking().FirstOrDefaultAsync(t => t.Id == techniqueId);
    }

    private async Task<int> CountTechniquesAsync()
    {
        await using var ctx = _db.CreateContext();
        return await ctx.Techniques.CountAsync();
    }

    private int SeedTechnique(string name = "Kimura", bool isActive = true)
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
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
        _db.Context.Techniques.Add(technique);
        _db.Context.SaveChanges();
        return technique.Id;
    }

    private Task<Technique> CreateAsync(string name = "Kimura") =>
        _sut.CreateAsync(name, "descripción", "objetivo", null, null, _beltId, _categoryId, _positionId);

    // ── Registro correcto de acciones administrativas ─────────────────────

    [Fact]
    public async Task CreateAsync_WithAdministrator_WritesSingleCompleteAuditEntry()
    {
        var technique = await CreateAsync("Kimura");

        var audits = await ReadAuditsAsync();
        audits.Should().ContainSingle();

        var entry = audits[0];
        entry.UserId.Should().Be(_adminId, "el actor debe ser el administrador responsable");
        entry.Action.Should().Be(AuditAction.Create);
        entry.EntityType.Should().Be("Technique");
        entry.EntityId.Should().Be(technique.Id);
        entry.Result.Should().Be(AuditResult.Success);
        entry.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(2));
        entry.ErrorMessage.Should().BeNull();
        entry.Details.Should().Contain("Kimura");
    }

    [Fact]
    public async Task UpdateAsync_WithAdministrator_WritesUpdateAuditEntry()
    {
        var techniqueId = SeedTechnique("Kimura");

        await _sut.UpdateAsync(techniqueId, "llave americana", "desc 2", "obj 2", null, null, _beltId, _categoryId, _positionId);

        var audits = await ReadAuditsAsync();
        audits.Should().ContainSingle();
        var entry = audits[0];
        entry.UserId.Should().Be(_adminId);
        entry.Action.Should().Be(AuditAction.Update);
        entry.EntityType.Should().Be("Technique");
        entry.EntityId.Should().Be(techniqueId);
        entry.Result.Should().Be(AuditResult.Success);
        entry.Details.Should().Contain("llave americana").And.Contain("Kimura");

        var stored = await ReadTechniqueAsync(techniqueId);
        stored!.Name.Should().Be("llave americana");
    }

    [Theory]
    [InlineData(false, AuditAction.SoftDelete)]
    [InlineData(true, AuditAction.Restore)]
    public async Task SetActiveAsync_WritesSingleStateChangeAuditEntry(bool isActive, AuditAction expectedAction)
    {
        var techniqueId = SeedTechnique("Triángulo", isActive: !isActive);

        await _sut.SetActiveAsync(techniqueId, isActive);

        var audits = await ReadAuditsAsync();
        audits.Should().ContainSingle();
        var entry = audits[0];
        entry.UserId.Should().Be(_adminId);
        entry.Action.Should().Be(expectedAction);
        entry.EntityType.Should().Be("Technique");
        entry.EntityId.Should().Be(techniqueId);
        entry.Result.Should().Be(AuditResult.Success);
        entry.Details.Should().Contain("IsActive").And.Contain(isActive ? "true" : "false");

        (await ReadTechniqueAsync(techniqueId))!.IsActive.Should().Be(isActive);
    }

    // ── Rechazo de usuarios sin autorización ──────────────────────────────

    [Fact]
    public async Task CreateAsync_WithoutSession_ThrowsAndRecordsSinglePermissionDenied()
    {
        _session.UserId = null;
        _session.Role = null;

        var act = async () => await CreateAsync("Técnica bloqueada");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        (await CountTechniquesAsync()).Should().Be(0, "no debe crearse nada sin sesión");

        var audits = await ReadAuditsAsync();
        audits.Should().ContainSingle("solo se registra un PermissionDenied por intento rechazado");
        var entry = audits[0];
        entry.Action.Should().Be(AuditAction.PermissionDenied);
        entry.Result.Should().Be(AuditResult.Failure);
        entry.UserId.Should().BeNull();
        entry.EntityType.Should().Be("Technique");
        entry.Details.Should().Contain(nameof(AuditAction.Create));
    }

    [Fact]
    public async Task SetActiveAsync_WithPractitioner_ThrowsKeepsStateAndRecordsPermissionDeniedOnce()
    {
        var techniqueId = SeedTechnique("Escapa de montada", isActive: true);
        _session.UserId = _practitionerId;
        _session.Role = UserRole.Practicante;

        var act = async () => await _sut.SetActiveAsync(techniqueId, false);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        var stored = await ReadTechniqueAsync(techniqueId);
        stored!.IsActive.Should().BeTrue("el practicante no puede desactivar técnicas");

        var audits = await ReadAuditsAsync();
        audits.Should().ContainSingle();
        audits[0].Action.Should().Be(AuditAction.PermissionDenied);
        audits[0].UserId.Should().Be(_practitionerId);
        audits[0].Result.Should().Be(AuditResult.Failure);
    }

    [Fact]
    public async Task UpdateAsync_WithPractitioner_ThrowsAndDoesNotModifyTechnique()
    {
        var techniqueId = SeedTechnique("Kimura");
        _session.UserId = _practitionerId;
        _session.Role = UserRole.Practicante;

        var act = async () => await _sut.UpdateAsync(techniqueId, "modificada", "d", "o", null, null, _beltId, _categoryId, _positionId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        (await ReadTechniqueAsync(techniqueId))!.Name.Should().Be("Kimura");
        (await ReadAuditsAsync()).Should().ContainSingle(a => a.Action == AuditAction.PermissionDenied);
    }

    // ── Sin auditorías de lectura ni duplicadas ───────────────────────────

    [Fact]
    public async Task ReadOperations_DoNotWriteAuditEntries()
    {
        var techniqueId = SeedTechnique("Lectura");

        await _sut.GetByIdAsync(techniqueId);
        await _sut.GetAllAsync();
        await _sut.GetAllForAdminAsync();
        await _sut.GetByBeltIdAsync(_beltId);
        await _sut.GetByCategoryIdAsync(_categoryId);
        await _sut.GetByPositionIdAsync(_positionId);

        (await ReadAuditsAsync()).Should().BeEmpty("las lecturas no se auditán automáticamente");
    }

    [Fact]
    public async Task UpdateAsync_WhenTechniqueDoesNotExist_WritesNoAuditEntry()
    {
        await _sut.UpdateAsync(999999, "x", "d", "o", null, null, _beltId, _categoryId, _positionId);

        (await ReadAuditsAsync()).Should().BeEmpty("no debe registrarse una operación que no modificó nada");
    }

    [Fact]
    public async Task AdminOperations_DoNotDuplicateAuditEntries()
    {
        var techniqueId = SeedTechnique("Kimura");

        await _sut.UpdateAsync(techniqueId, "Kimura v2", "d", "o", null, null, _beltId, _categoryId, _positionId);
        await _sut.SetActiveAsync(techniqueId, false);

        var audits = await ReadAuditsAsync();
        audits.Should().HaveCount(2, "una entrada por operación administrativa ejecutada");
        audits.Select(a => a.Action).Should().OnlyHaveUniqueItems();
    }

    // ── Reversión ante fallo crítico ──────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WhenAuditWriteFails_RollsBackTechniqueCreation()
    {
        _db.Execute("DROP TABLE AuditLogs;");

        var act = async () => await CreateAsync("No debe persistir");

        await act.Should().ThrowAsync<Exception>("la auditoría estricta propaga el fallo");
        (await CountTechniquesAsync()).Should().Be(0, "la modificación debe revertirse junto con su auditoría");
    }

    [Fact]
    public async Task SetActiveAsync_WhenAuditWriteFails_KeepsTechniqueUnchanged()
    {
        var techniqueId = SeedTechnique("Kimura", isActive: true);
        _db.Execute("DROP TABLE AuditLogs;");

        var act = async () => await _sut.SetActiveAsync(techniqueId, false);

        await act.Should().ThrowAsync<Exception>();
        (await ReadTechniqueAsync(techniqueId))!.IsActive.Should().BeTrue("sin auditoría no hay modificación exitosa");
    }

    [Fact]
    public async Task UpdateAsync_WhenAuditWriteFails_KeepsPreviousValues()
    {
        var techniqueId = SeedTechnique("Kimura");
        _db.Execute("DROP TABLE AuditLogs;");

        var act = async () => await _sut.UpdateAsync(techniqueId, "Nombre nuevo", "d", "o", null, null, _beltId, _categoryId, _positionId);

        await act.Should().ThrowAsync<Exception>();
        (await ReadTechniqueAsync(techniqueId))!.Name.Should().Be("Kimura");
    }

    // ── Información sensible ──────────────────────────────────────────────

    [Fact]
    public async Task AdminOperations_NeverWriteSecretsToAuditLog()
    {
        var techniqueId = SeedTechnique("Escape de montada");
        await CreateAsync("Otra técnica");
        await _sut.UpdateAsync(techniqueId, "Escape de montada v2", "d", "o", null, null, _beltId, _categoryId, _positionId);
        await _sut.SetActiveAsync(techniqueId, false);

        _session.UserId = _practitionerId;
        _session.Role = UserRole.Practicante;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateAsync("Bloqueada"));

        var audits = await ReadAuditsAsync();
        audits.Should().NotBeEmpty();

        foreach (var entry in audits)
        {
            var blob = $"{entry.Details} {entry.ErrorMessage}";

            blob.Should().NotContain(SecretHash);
            blob.Should().NotContain(SecretSalt);
            blob.Should().NotContain("PasswordHash");
            blob.Should().NotContain("PasswordSalt");
            blob.Should().NotContain("password");
            blob.Should().NotContain("token");
            blob.Should().NotContain("secret");
        }
    }
}
