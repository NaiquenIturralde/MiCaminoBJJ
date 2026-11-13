using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

/// <summary>
/// Multimedia (Etapa 1.2: auditoría administrativa + 1.2.1: integridad).
///
/// Operaciones administrativas auditadas (UN registro por acción de usuario):
/// - AddOfficialMediaFromFileAsync / FromAssetAsync -> Create   (alta atómica)
/// - AddOfficialMediaAsync                          -> Create   (vínculo)
/// - RemoveOfficialMediaAsync                       -> SoftDelete o Update (baja atómica)
/// - SoftDeleteAsync                                -> SoftDelete (eliminación lógica)
/// - SetCoverAsync                                  -> Update    (portada)
///
/// Integridad de archivos (Etapa 1.2.1):
/// - La autorización corre ANTES de escribir ningún archivo en disco.
/// - El alta (archivo + recurso + vínculo + auditoría) es una sola transacción;
///   si la base falla después de guardar el archivo, éste se compensa (borrado).
/// - La baja desvincula y decide la eliminación lógica según queden
///   referencias válidas; el borrado físico ocurre SOLO después del commit y
///   únicamente si el archivo ya no tiene referencias ni es compartido.
///
/// CreateAsync queda genérico (puede servir a flujos personales) y las
/// operaciones de los practicantes (recursos personales, foto de perfil,
/// consultas) no se auditán ni cambian de comportamiento.
/// </summary>
public class MediaService : IMediaService
{
    private const string EntityName = nameof(MediaAsset);

    private readonly AppDbContext _context;
    private readonly ILogger<MediaService> _logger;
    private readonly IAuditService _audit;
    private readonly IAdminAuthorizationService _authorization;
    private readonly IMediaStorageService _storage;

    public MediaService(
        AppDbContext context,
        ILogger<MediaService> logger,
        IAuditService audit,
        IAdminAuthorizationService authorization,
        IMediaStorageService storage)
    {
        _context = context;
        _logger = logger;
        _audit = audit;
        _authorization = authorization;
        _storage = storage;
    }

    /// <summary>
    /// Crea el recurso. Se exige el rol Administrador porque hoy solo el panel
    /// admin incorpora recursos oficiales (los flujos personales no están
    /// implementados). No escribe auditoría propia: la acción visible queda
    /// registrada una única vez en AddOfficialMediaAsync (incorporación), para
    /// no duplicar registros por una misma acción de usuario.
    /// </summary>
    public async Task<MediaAsset> CreateAsync(MediaAsset mediaAsset)
    {
        return await AdminAuditOperation.ExecuteAsync(
            _context,
            _authorization,
            AuditAction.Create,
            EntityName,
            entityId: 0, // el recurso todavía no existe
            operation: async _ =>
            {
                _context.MediaAssets.Add(mediaAsset);
                await _context.SaveChangesAsync();
                return mediaAsset;
            },
            auditAsync: _ => Task.CompletedTask);
    }

    public async Task<MediaAsset?> GetByIdAsync(int mediaAssetId)
    {
        return await _context.MediaAssets
            .FirstOrDefaultAsync(m => m.Id == mediaAssetId && !m.IsDeleted);
    }

    public async Task SoftDeleteAsync(int mediaAssetId)
    {
        var applied = false;
        MediaAsset? media = null;

        await AdminAuditOperation.ExecuteAsync(
            _context,
            _authorization,
            AuditAction.SoftDelete,
            EntityName,
            mediaAssetId,
            operation: async _ =>
            {
                media = await _context.MediaAssets.FindAsync(mediaAssetId);
                if (media == null)
                    return false;

                // Idempotente: si ya estaba dada de baja no se repite la
                // modificación ni se escribe una auditoría duplicada.
                if (media.IsDeleted)
                    return false;

                media.IsDeleted = true;
                media.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                applied = true;
                return true;
            },
            auditAsync: actorId => !applied
                ? Task.CompletedTask
                : _audit.RecordStrictAsync(
                    actorId,
                    AuditAction.SoftDelete,
                    EntityName,
                    mediaAssetId,
                    changes: new Dictionary<string, object?> { ["IsDeleted"] = true }));

        // Borrado físico solo si la baja quedó confirmada (commit) y el
        // archivo ya no tiene ninguna referencia válida.
        if (applied && media?.LocalPath is not null && _context.Database.CurrentTransaction is null)
            await PurgeFileIfUnreferencedAsync(media);
    }

    public async Task<List<MediaAsset>> GetOfficialByTechniqueAsync(int techniqueId)
    {
        return await _context.TechniqueMedia
            .Include(tm => tm.MediaAsset)
            .Where(tm => tm.TechniqueId == techniqueId && !tm.MediaAsset.IsDeleted)
            .OrderBy(tm => tm.DisplayOrder)
            .Select(tm => tm.MediaAsset)
            .ToListAsync();
    }

    public async Task<List<TechniqueMedia>> GetOfficialMediaEntriesAsync(int techniqueId)
    {
        return await _context.TechniqueMedia
            .Include(tm => tm.MediaAsset)
            .Where(tm => tm.TechniqueId == techniqueId && !tm.MediaAsset.IsDeleted)
            .OrderBy(tm => tm.DisplayOrder)
            .ToListAsync();
    }

    public async Task<List<MediaAsset>> GetPersonalByTechniqueAsync(int userId, int techniqueId)
    {
        return await _context.UserTechniqueResources
            .Include(r => r.MediaAsset)
            .Where(r => r.UserId == userId && r.TechniqueId == techniqueId && !r.MediaAsset.IsDeleted)
            .OrderBy(r => r.DisplayOrder)
            .Select(r => r.MediaAsset)
            .ToListAsync();
    }

    public async Task<TechniqueMedia> AddOfficialMediaAsync(int techniqueId, int mediaAssetId, bool isCover, int displayOrder)
    {
        var entry = new TechniqueMedia
        {
            TechniqueId = techniqueId,
            MediaAssetId = mediaAssetId,
            IsCover = isCover,
            DisplayOrder = displayOrder
        };

        MediaAsset? media = null;

        return await AdminAuditOperation.ExecuteAsync(
            _context,
            _authorization,
            AuditAction.Create,
            EntityName,
            mediaAssetId,
            operation: async _ =>
            {
                media = await _context.MediaAssets.FindAsync(mediaAssetId);

                _context.TechniqueMedia.Add(entry);
                await _context.SaveChangesAsync();
                return entry;
            },
            auditAsync: actorId => _audit.RecordStrictAsync(
                actorId,
                AuditAction.Create,
                EntityName,
                mediaAssetId,
                changes: new Dictionary<string, object?>
                {
                    ["TechniqueId"] = techniqueId,
                    ["MediaType"] = media?.MediaType.ToString(),
                    ["SourceType"] = media?.SourceType.ToString(),
                    ["IsCover"] = isCover,
                    ["DisplayOrder"] = displayOrder
                }));
    }

    /// <summary>
    /// Alta administrativa ATÓMICA desde archivo local (Etapa 1.2.1):
    /// 1) autorización (rol Administrador) ANTES de escribir en disco,
    /// 2) archivo con nombre único (propio de esta operación),
    /// 3) recurso + vínculo + UNA auditoría en la misma transacción,
    /// 4) si algo falla después de guardar el archivo, éste se elimina
    ///    (compensación) para no dejar archivos huérfanos.
    /// </summary>
    public async Task<TechniqueMedia> AddOfficialMediaFromFileAsync(
        int techniqueId, Stream fileStream, string originalFileName, MediaType mediaType, bool isCover, int displayOrder)
    {
        var validation = _storage.ValidateFile(originalFileName, fileStream.Length, fileStream);
        if (!validation.IsValid)
            throw new InvalidOperationException(validation.ErrorMessage ?? "El archivo no es válido.");

        string? savedPath = null;
        MediaAsset? createdAsset = null;

        try
        {
            return await AdminAuditOperation.ExecuteAsync(
                _context,
                _authorization,
                AuditAction.Create,
                EntityName,
                entityId: 0, // el recurso todavía no existe
                operation: async actorId =>
                {
                    savedPath = await _storage.SaveFileAsync(
                        fileStream, originalFileName, mediaType, MediaSourceType.PackagedResource, actorId, techniqueId);

                    createdAsset = new MediaAsset
                    {
                        UploadedByUserId = actorId,
                        MediaType = mediaType,
                        SourceType = MediaSourceType.PackagedResource,
                        LocalPath = savedPath,
                        OriginalFileName = originalFileName,
                        MimeType = validation.MimeType,
                        FileSizeBytes = validation.FileSizeBytes,
                        Title = originalFileName,
                        SyncStatus = SyncStatus.LocalOnly
                    };

                    return await CreateAssetAndLinkAsync(createdAsset, techniqueId, isCover, displayOrder);
                },
                auditAsync: actorId => _audit.RecordStrictAsync(
                    actorId,
                    AuditAction.Create,
                    EntityName,
                    createdAsset!.Id,
                    changes: new Dictionary<string, object?>
                    {
                        ["TechniqueId"] = techniqueId,
                        ["MediaType"] = mediaType.ToString(),
                        ["FileName"] = originalFileName,
                        ["IsCover"] = isCover,
                        ["DisplayOrder"] = displayOrder
                    }));
        }
        catch
        {
            // Compensación: la base o la auditoría fallaron DESPUÉS de guardar
            // el archivo => se retira para no dejar un huérfano en disco.
            if (savedPath is not null)
                await TryDeleteFileQuietlyAsync(savedPath, "compensación de alta administrativa");
            throw;
        }
    }

    /// <summary>
    /// Alta administrativa ATÓMICA de un recurso ya construido (enlace externo):
    /// crea el recurso, lo vincula y audita una única vez en la misma transacción.
    /// </summary>
    public async Task<TechniqueMedia> AddOfficialMediaFromAssetAsync(
        MediaAsset mediaAsset, int techniqueId, bool isCover, int displayOrder)
    {
        return await AdminAuditOperation.ExecuteAsync(
            _context,
            _authorization,
            AuditAction.Create,
            EntityName,
            entityId: 0,
            operation: async actorId =>
            {
                // El dueño real del recurso es el administrador autorizado.
                mediaAsset.UploadedByUserId = actorId;
                return await CreateAssetAndLinkAsync(mediaAsset, techniqueId, isCover, displayOrder);
            },
            auditAsync: actorId => _audit.RecordStrictAsync(
                actorId,
                AuditAction.Create,
                EntityName,
                mediaAsset.Id,
                changes: new Dictionary<string, object?>
                {
                    ["TechniqueId"] = techniqueId,
                    ["MediaType"] = mediaAsset.MediaType.ToString(),
                    ["SourceType"] = mediaAsset.SourceType.ToString(),
                    ["IsCover"] = isCover,
                    ["DisplayOrder"] = displayOrder
                }));
    }

    /// <summary>
    /// Baja administrativa ATÓMICA (Etapa 1.2.1): desvincula la técnica y, solo
    /// si era la última referencia válida, aplica la eliminación lógica. Escribe
    /// UNA sola auditoría por acción y, después del commit, elimina físicamente
    /// el archivo únicamente si quedó sin referencias y sin compartirse.
    /// Idempotente: repetir la acción no genera auditorías duplicadas.
    /// </summary>
    public async Task RemoveOfficialMediaAsync(int techniqueId, int mediaAssetId)
    {
        var applied = false;
        var softDeleted = false;
        var remainingReferences = 0;
        MediaAsset? asset = null;
        var resultAction = AuditAction.SoftDelete;

        await AdminAuditOperation.ExecuteAsync(
            _context,
            _authorization,
            AuditAction.SoftDelete,
            EntityName,
            mediaAssetId,
            operation: async _ =>
            {
                asset = await _context.MediaAssets.FindAsync(mediaAssetId);

                var link = await _context.TechniqueMedia
                    .FirstOrDefaultAsync(tm => tm.TechniqueId == techniqueId && tm.MediaAssetId == mediaAssetId);

                // Sin vínculo no hay operación: la acción repetida no audita.
                if (link is null)
                    return false;

                _context.TechniqueMedia.Remove(link);

                // Excluye el vínculo que se retira (todavía sin confirmar).
                remainingReferences = await CountOtherReferencesAsync(mediaAssetId, link.Id);

                if (asset is not null && !asset.IsDeleted && remainingReferences == 0)
                {
                    // Última referencia: eliminación lógica del recurso.
                    asset.IsDeleted = true;
                    asset.UpdatedAt = DateTime.UtcNow;
                    softDeleted = true;
                }

                // Si sigue referenciado (otra técnica, recurso personal o
                // perfil), solo se desvincula: ni baja lógica ni borrado físico.
                resultAction = softDeleted ? AuditAction.SoftDelete : AuditAction.Update;

                await _context.SaveChangesAsync();
                applied = true;
                return true;
            },
            auditAsync: actorId => !applied
                ? Task.CompletedTask
                : _audit.RecordStrictAsync(
                    actorId,
                    resultAction,
                    EntityName,
                    mediaAssetId,
                    changes: new Dictionary<string, object?>
                    {
                        ["TechniqueId"] = techniqueId,
                        ["IsDeleted"] = softDeleted,
                        ["RemainingReferences"] = remainingReferences
                    }));

        // Borrado físico SOLO después del commit (sin transacción abierta) y
        // solo si el archivo quedó sin referencias.
        if (softDeleted && asset?.LocalPath is not null && _context.Database.CurrentTransaction is null)
            await PurgeFileIfUnreferencedAsync(asset);
    }

    /// <summary>Crea el recurso y su vínculo oficial dentro de la transacción activa.</summary>
    private async Task<TechniqueMedia> CreateAssetAndLinkAsync(MediaAsset mediaAsset, int techniqueId, bool isCover, int displayOrder)
    {
        _context.MediaAssets.Add(mediaAsset);
        await _context.SaveChangesAsync();

        var entry = new TechniqueMedia
        {
            TechniqueId = techniqueId,
            MediaAssetId = mediaAsset.Id,
            IsCover = isCover,
            DisplayOrder = displayOrder
        };

        _context.TechniqueMedia.Add(entry);
        await _context.SaveChangesAsync();
        return entry;
    }

    /// <summary>Referencias vivas al recurso distintas del vínculo que se retira.</summary>
    private async Task<int> CountOtherReferencesAsync(int mediaAssetId, int excludedLinkId) =>
        await _context.TechniqueMedia.CountAsync(tm => tm.MediaAssetId == mediaAssetId && tm.Id != excludedLinkId)
        + await _context.UserTechniqueResources.CountAsync(r => r.MediaAssetId == mediaAssetId)
        + await _context.UserProfileMedia.CountAsync(pm => pm.MediaAssetId == mediaAssetId);

    /// <summary>
    /// Borra el archivo SOLO si ninguna entidad válida lo referencia y ningún
    /// otro recurso comparte la misma ruta.
    /// </summary>
    private async Task PurgeFileIfUnreferencedAsync(MediaAsset asset)
    {
        var path = asset.LocalPath;
        if (string.IsNullOrEmpty(path))
            return;

        if (await _context.MediaAssets.AnyAsync(m => m.Id != asset.Id && m.LocalPath == path))
            return;

        var referenced = await _context.TechniqueMedia.AnyAsync(tm => tm.MediaAssetId == asset.Id)
            || await _context.UserTechniqueResources.AnyAsync(r => r.MediaAssetId == asset.Id)
            || await _context.UserProfileMedia.AnyAsync(pm => pm.MediaAssetId == asset.Id);

        if (referenced)
            return;

        await TryDeleteFileQuietlyAsync(path, "purga tras eliminación lógica sin referencias");
    }

    private async Task TryDeleteFileQuietlyAsync(string relativePath, string reason)
    {
        try
        {
            await _storage.DeleteFileAsync(relativePath);
        }
        catch (Exception ex)
        {
            // Preferible conservar un archivo de más que perder datos: el
            // recurso ya está dado de baja o nunca llegó a persistirse.
            _logger.LogWarning(ex, "No se pudo eliminar el archivo '{Path}' ({Reason}).", relativePath, reason);
        }
    }

    public async Task SetCoverAsync(int techniqueId, int mediaAssetId)
    {
        var applied = false;

        await AdminAuditOperation.ExecuteAsync(
            _context,
            _authorization,
            AuditAction.Update,
            EntityName,
            mediaAssetId,
            operation: async _ =>
            {
                // Unset any current cover for this technique
                var currentCover = await _context.TechniqueMedia
                    .Where(tm => tm.TechniqueId == techniqueId && tm.IsCover)
                    .ToListAsync();

                foreach (var item in currentCover)
                {
                    item.IsCover = false;
                }

                // Set the new cover if the entry exists
                var entry = await _context.TechniqueMedia
                    .FirstOrDefaultAsync(tm => tm.TechniqueId == techniqueId && tm.MediaAssetId == mediaAssetId);

                if (entry != null)
                {
                    entry.IsCover = true;
                    applied = true;
                }

                await _context.SaveChangesAsync();
                return applied;
            },
            auditAsync: actorId => !applied
                ? Task.CompletedTask
                : _audit.RecordStrictAsync(
                    actorId,
                    AuditAction.Update,
                    EntityName,
                    mediaAssetId,
                    changes: new Dictionary<string, object?>
                    {
                        ["TechniqueId"] = techniqueId,
                        ["IsCover"] = true
                    }));
    }

    public async Task<UserTechniqueResource> AddPersonalResourceAsync(int userId, int techniqueId, int mediaAssetId, string? notes, int displayOrder)
    {
        var resource = new UserTechniqueResource
        {
            UserId = userId,
            TechniqueId = techniqueId,
            MediaAssetId = mediaAssetId,
            PersonalNotes = notes,
            DisplayOrder = displayOrder
        };

        _context.UserTechniqueResources.Add(resource);
        await _context.SaveChangesAsync();
        return resource;
    }

    public async Task<UserProfileMedia> AddProfileMediaAsync(int userId, int mediaAssetId, bool isProfilePicture)
    {
        var media = new UserProfileMedia
        {
            UserId = userId,
            MediaAssetId = mediaAssetId,
            IsProfilePicture = isProfilePicture
        };

        _context.UserProfileMedia.Add(media);
        await _context.SaveChangesAsync();
        return media;
    }

    public async Task SetProfilePictureAsync(int userId, int mediaAssetId)
    {
        // Unset current profile picture
        var current = await _context.UserProfileMedia
            .Where(pm => pm.UserId == userId && pm.IsProfilePicture)
            .ToListAsync();

        foreach (var item in current)
        {
            item.IsProfilePicture = false;
        }

        // Check if this media is already associated
        var existing = await _context.UserProfileMedia
            .FirstOrDefaultAsync(pm => pm.UserId == userId && pm.MediaAssetId == mediaAssetId);

        if (existing != null)
        {
            existing.IsProfilePicture = true;
        }
        else
        {
            _context.UserProfileMedia.Add(new UserProfileMedia
            {
                UserId = userId,
                MediaAssetId = mediaAssetId,
                IsProfilePicture = true
            });
        }

        await _context.SaveChangesAsync();
    }
}
