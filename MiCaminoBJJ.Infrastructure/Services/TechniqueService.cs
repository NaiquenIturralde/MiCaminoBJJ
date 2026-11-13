using Microsoft.EntityFrameworkCore;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

/// <summary>
/// Catálogo de técnicas (Etapa 1.2: auditoría administrativa).
///
/// Escrituras administrativas (Create, Update, SetActive):
/// - exigen sesión autenticada con rol Administrador (IAdminAuthorizationService),
/// - se ejecutan en transacción junto con su registro de auditoría estricto,
///   por lo que una auditoría fallida revierte la modificación completa,
/// - dejan UN solo AuditLog por operación (sin duplicados).
///
/// Lecturas y cualquier uso de los practicantes conservan el comportamiento
/// previo: sin guardia y sin auditoría automática.
/// </summary>
public class TechniqueService : ITechniqueService
{
    private const string EntityName = nameof(Technique);

    private readonly AppDbContext _context;
    private readonly IAuditService _audit;
    private readonly IAdminAuthorizationService _authorization;

    public TechniqueService(AppDbContext context, IAuditService audit, IAdminAuthorizationService authorization)
    {
        _context = context;
        _audit = audit;
        _authorization = authorization;
    }

    public async Task<Technique?> GetByIdAsync(int techniqueId)
    {
        return await _context.Techniques
            .Include(t => t.Belt)
            .Include(t => t.Category)
            .Include(t => t.Position)
            .Include(t => t.OfficialMedia).ThenInclude(tm => tm.MediaAsset)
            .FirstOrDefaultAsync(t => t.Id == techniqueId && t.IsActive);
    }

    public async Task<List<Technique>> GetAllAsync()
    {
        return await _context.Techniques
            .Include(t => t.Belt)
            .Include(t => t.Category)
            .Include(t => t.Position)
            .Where(t => t.IsActive)
            .OrderBy(t => t.Belt.DisplayOrder)
            .ThenBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<List<Technique>> GetAllForAdminAsync()
    {
        return await _context.Techniques
            .Include(t => t.Belt)
            .Include(t => t.Category)
            .Include(t => t.Position)
            .OrderBy(t => t.IsActive)
            .ThenBy(t => t.Belt.DisplayOrder)
            .ThenBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<List<Technique>> GetByBeltIdAsync(int beltId)
    {
        return await _context.Techniques
            .Include(t => t.Belt)
            .Include(t => t.Category)
            .Include(t => t.Position)
            .Where(t => t.BeltId == beltId && t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<List<Technique>> GetByCategoryIdAsync(int categoryId)
    {
        return await _context.Techniques
            .Include(t => t.Belt)
            .Include(t => t.Category)
            .Include(t => t.Position)
            .Where(t => t.CategoryId == categoryId && t.IsActive)
            .ToListAsync();
    }

    public async Task<List<Technique>> GetByPositionIdAsync(int positionId)
    {
        return await _context.Techniques
            .Include(t => t.Belt)
            .Include(t => t.Category)
            .Include(t => t.Position)
            .Where(t => t.PositionId == positionId && t.IsActive)
            .ToListAsync();
    }

    public async Task<Technique?> GetLastViewedByUserAsync(int userId)
    {
        var lastView = await _context.TechniqueViews
            .Where(tv => tv.UserId == userId)
            .OrderByDescending(tv => tv.ViewedAt)
            .FirstOrDefaultAsync();

        if (lastView == null)
            return null;

        return await GetByIdAsync(lastView.TechniqueId);
    }

    public async Task<Technique> CreateAsync(string name, string description, string objective, string? steps, string? tips, int beltId, int categoryId, int positionId)
    {
        var technique = new Technique
        {
            Name = name,
            NameNormalized = TextNormalizer.Normalize(name),
            Description = description,
            Objective = objective,
            Steps = steps,
            Tips = tips,
            BeltId = beltId,
            CategoryId = categoryId,
            PositionId = positionId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        return await AdminAuditOperation.ExecuteAsync(
            _context,
            _authorization,
            AuditAction.Create,
            EntityName,
            entityId: 0, // el registro todavía no existe
            operation: async _ =>
            {
                _context.Techniques.Add(technique);
                await _context.SaveChangesAsync();
                return technique;
            },
            auditAsync: actorId => _audit.RecordStrictAsync(
                actorId,
                AuditAction.Create,
                EntityName,
                technique.Id,
                changes: new Dictionary<string, object?>
                {
                    ["Name"] = technique.Name,
                    ["BeltId"] = technique.BeltId,
                    ["CategoryId"] = technique.CategoryId,
                    ["PositionId"] = technique.PositionId,
                    ["IsActive"] = technique.IsActive
                }));
    }

    public async Task UpdateAsync(int techniqueId, string name, string description, string objective, string? steps, string? tips, int beltId, int categoryId, int positionId)
    {
        var updated = false;
        string? previousName = null;
        int previousBeltId = 0, previousCategoryId = 0, previousPositionId = 0;

        await AdminAuditOperation.ExecuteAsync(
            _context,
            _authorization,
            AuditAction.Update,
            EntityName,
            techniqueId,
            operation: async _ =>
            {
                var technique = await _context.Techniques.FindAsync(techniqueId);
                if (technique == null)
                    return false;

                previousName = technique.Name;
                previousBeltId = technique.BeltId;
                previousCategoryId = technique.CategoryId;
                previousPositionId = technique.PositionId;

                technique.Name = name;
                technique.NameNormalized = TextNormalizer.Normalize(name);
                technique.Description = description;
                technique.Objective = objective;
                technique.Steps = steps;
                technique.Tips = tips;
                technique.BeltId = beltId;
                technique.CategoryId = categoryId;
                technique.PositionId = positionId;
                technique.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                updated = true;
                return true;
            },
            auditAsync: actorId => !updated
                ? Task.CompletedTask // no hay entidad: nada que auditar
                : _audit.RecordStrictAsync(
                    actorId,
                    AuditAction.Update,
                    EntityName,
                    techniqueId,
                    changes: new Dictionary<string, object?>
                    {
                        ["OldName"] = previousName,
                        ["Name"] = name,
                        ["OldBeltId"] = previousBeltId,
                        ["BeltId"] = beltId,
                        ["OldCategoryId"] = previousCategoryId,
                        ["CategoryId"] = categoryId,
                        ["OldPositionId"] = previousPositionId,
                        ["PositionId"] = positionId
                    }));
    }

    public async Task SetActiveAsync(int techniqueId, bool isActive)
    {
        // Ocultar = eliminación lógica; restaurar = reversión de esa eliminación.
        var action = isActive ? AuditAction.Restore : AuditAction.SoftDelete;
        var applied = false;

        await AdminAuditOperation.ExecuteAsync(
            _context,
            _authorization,
            action,
            EntityName,
            techniqueId,
            operation: async _ =>
            {
                var technique = await _context.Techniques.FindAsync(techniqueId);
                if (technique == null)
                    return false;

                technique.IsActive = isActive;
                technique.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                applied = true;
                return true;
            },
            auditAsync: actorId => !applied
                ? Task.CompletedTask
                : _audit.RecordStrictAsync(
                    actorId,
                    action,
                    EntityName,
                    techniqueId,
                    changes: new Dictionary<string, object?> { ["IsActive"] = isActive }));
    }
}
