using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Web.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MiCaminoBJJ.Web.Services.Admin;

/// <summary>
/// Comandos multimedia administrativos sobre SQL Server (Web), con las MISMAS
/// garantías que <see cref="AdminTecnicasCommandService"/>: transacción única,
/// auditoría estricta (se revierte todo si falla) y sin eliminaciones físicas
/// de recursos compartidos.
///
/// Validación de servidor:
/// - URL obligatoria en ALTA, opcional en EDICIÓN, parseada con
///   <see cref="IExternalMediaParser"/> (YouTube watch/shorts/youtu.be/embed y
///   Vimeo). El video NUNCA se descarga: solo se guardan URL, id y proveedor.
/// - Límites reales del dominio: URL 2000, título 300, descripción 1000 y un
///   máximo de <see cref="MaxRecursosPorTecnica"/> recursos por técnica.
/// - Duplicados por (proveedor, videoId) o por URL exacta dentro de la MISMA
///   técnica.
///
/// Autoría (<c>MediaAsset.UploadedByUserId</c>, FK obligatoria): se resuelve el
/// usuario de dominio por email del actor (sesión Identity) o, en ausencia, el
/// perfil de dominio con rol Administrador. Sin usuario de dominio el alta se
/// rechaza con mensaje claro (no se crean usuarios ficticios).
/// </summary>
public sealed class AdminTecnicasMediaCommandService : IAdminTecnicasMediaCommandService
{
    /// <summary>Límite de recursos multimedia por técnica (validado en servidor).</summary>
    public const int MaxRecursosPorTecnica = 10;

    private const int MaxUrlLength = 2000;
    private const int MaxTituloLength = 300;
    private const int MaxDescripcionLength = 1000;

    private const string EntityTechniqueMedia = nameof(TechniqueMedia);
    private const string EntityMediaAsset = nameof(MediaAsset);

    private readonly WebTfDbContext _db;
    private readonly IAuditService _audit;
    private readonly IExternalMediaParser _parser;

    public AdminTecnicasMediaCommandService(
        WebTfDbContext db,
        IAuditService audit,
        IExternalMediaParser parser)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    // ── Alta ───────────────────────────────────────────────────────────────

    public async Task<ResultadoOperacion> AgregarAsync(int tecnicaId, MediaComando comando, string actor)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (!await _db.Techniques.AnyAsync(t => t.Id == tecnicaId))
            return ResultadoOperacion.NoEncontrada();

        var errores = await ValidarAsync(comando, tecnicaId, mediaIdExcluido: null, urlObligatoria: true);
        if (errores.Count > 0)
            return ResultadoOperacion.Fallo(errores);

        var url = comando.Url!.Trim();
        var parsed = _parser.ParseUrl(url);

        var autorId = await ResolverUsuarioAutorAsync(actor);
        if (autorId is null)
            return ResultadoOperacion.Fallo(
                "No hay un usuario administrador de dominio para adjuntar el recurso " +
                "(verificá el perfil admin@micaminobjj.com).");

        await using var transaccion = await _db.Database.BeginTransactionAsync();

        var asset = new MediaAsset
        {
            UploadedByUserId = autorId.Value,
            MediaType = MediaType.Video,
            SourceType = MediaSourceType.ExternalLink,
            ExternalUrl = url,
            ExternalProvider = parsed.Provider,
            ExternalVideoId = parsed.VideoId,
            Title = string.IsNullOrWhiteSpace(comando.Titulo) ? TituloPorDefecto(parsed) : comando.Titulo.Trim(),
            Description = comando.Descripcion?.Trim(),
            SyncStatus = SyncStatus.LocalOnly,
            CreatedAt = DateTime.UtcNow
        };

        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync();

        var tienePortada = await _db.TechniqueMedia
            .AnyAsync(tm => tm.TechniqueId == tecnicaId && tm.IsCover);

        var maxOrden = await _db.TechniqueMedia
            .Where(tm => tm.TechniqueId == tecnicaId)
            .MaxAsync(tm => (int?)tm.DisplayOrder) ?? 0;

        var link = new TechniqueMedia
        {
            TechniqueId = tecnicaId,
            MediaAssetId = asset.Id,
            IsCover = !tienePortada,
            DisplayOrder = maxOrden + 1,
            CreatedAt = DateTime.UtcNow
        };

        _db.TechniqueMedia.Add(link);
        await _db.SaveChangesAsync();

        await _audit.RecordStrictAsync(
            userId: null,
            AuditAction.Create,
            EntityTechniqueMedia,
            link.Id,
            changes: new Dictionary<string, object?>
            {
                ["Actor"] = actor,
                ["TechniqueId"] = tecnicaId,
                ["MediaAssetId"] = asset.Id,
                ["Url"] = asset.ExternalUrl,
                ["Provider"] = asset.ExternalProvider.ToString(),
                ["Titulo"] = asset.Title,
                ["IsCover"] = link.IsCover,
                ["DisplayOrder"] = link.DisplayOrder
            });

        await transaccion.CommitAsync();
        return ResultadoOperacion.Ok(tecnicaId);
    }

    // ── Edición ────────────────────────────────────────────────────────────

    public async Task<ResultadoOperacion> EditarAsync(int tecnicaId, int mediaId, MediaComando comando, string actor)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var errores = await ValidarAsync(comando, tecnicaId, mediaId, urlObligatoria: false);
        if (errores.Count > 0)
            return ResultadoOperacion.Fallo(errores);

        await using var transaccion = await _db.Database.BeginTransactionAsync();

        var link = await _db.TechniqueMedia
            .Include(tm => tm.MediaAsset)
            .FirstOrDefaultAsync(tm => tm.Id == mediaId && tm.TechniqueId == tecnicaId);

        if (link is null)
            return ResultadoOperacion.NoEncontrada();

        var asset = link.MediaAsset;
        var urlAnterior = asset.ExternalUrl;
        var tituloAnterior = asset.Title;
        var fueClonado = false;

        // Escritura con copia: si el recurso lo comparten OTRA técnica, un
        // recurso personal o un perfil, se clona para esta técnica y se edita
        // la copia — nadie más ve mutado su recurso.
        if (await EstaCompartidoAsync(asset.Id, tecnicaId, mediaId))
        {
            var copia = ClonarAsset(asset, autorId: await ResolverUsuarioAutorAsync(actor));
            _db.MediaAssets.Add(copia);
            await _db.SaveChangesAsync();
            asset = copia;
            fueClonado = true;
        }

        asset.Title = string.IsNullOrWhiteSpace(comando.Titulo) ? null : comando.Titulo.Trim();
        asset.Description = comando.Descripcion?.Trim();

        var urlNueva = comando.Url?.Trim();
        if (!string.IsNullOrEmpty(urlNueva) && !string.Equals(urlNueva, asset.ExternalUrl, StringComparison.Ordinal))
        {
            var parsed = _parser.ParseUrl(urlNueva);
            asset.ExternalUrl = urlNueva;
            asset.ExternalProvider = parsed.Provider;
            asset.ExternalVideoId = parsed.VideoId;
            asset.MediaType = MediaType.Video;
            asset.SourceType = MediaSourceType.ExternalLink;
        }

        asset.UpdatedAt = DateTime.UtcNow;

        // Re-apunta el vínculo cuando hubo clonación (el índice único
        // TechniqueId+MediaAssetId lo permite: es un Id nuevo).
        if (link.MediaAssetId != asset.Id)
            link.MediaAssetId = asset.Id;

        await _db.SaveChangesAsync();

        await _audit.RecordStrictAsync(
            userId: null,
            AuditAction.Update,
            EntityMediaAsset,
            asset.Id,
            changes: new Dictionary<string, object?>
            {
                ["Actor"] = actor,
                ["TechniqueMediaId"] = link.Id,
                ["TechniqueId"] = tecnicaId,
                ["OldUrl"] = urlAnterior,
                ["Url"] = asset.ExternalUrl,
                ["OldTitulo"] = tituloAnterior,
                ["Titulo"] = asset.Title,
                ["Clonado"] = fueClonado
            });

        await transaccion.CommitAsync();
        return ResultadoOperacion.Ok(tecnicaId);
    }

    // ── Portada ────────────────────────────────────────────────────────────

    public async Task<ResultadoOperacion> MarcarPortadaAsync(int tecnicaId, int mediaId, string actor)
    {
        await using var transaccion = await _db.Database.BeginTransactionAsync();

        var link = await _db.TechniqueMedia
            .FirstOrDefaultAsync(tm => tm.Id == mediaId && tm.TechniqueId == tecnicaId);

        if (link is null)
            return ResultadoOperacion.NoEncontrada();

        var yaEsPortada = link.IsCover &&
            !await _db.TechniqueMedia.AnyAsync(tm =>
                tm.TechniqueId == tecnicaId && tm.Id != mediaId && tm.IsCover);

        if (yaEsPortada)
        {
            await transaccion.CommitAsync();
            return ResultadoOperacion.Ok(tecnicaId); // idempotente: sin auditoría duplicada
        }

        var vinculos = await _db.TechniqueMedia
            .Where(tm => tm.TechniqueId == tecnicaId)
            .ToListAsync();

        var portadasAnteriores = vinculos
            .Where(tm => tm.IsCover && tm.Id != mediaId)
            .Select(tm => tm.Id)
            .ToList();

        // Regla de negocio: un SOLO recurso portada por técnica.
        foreach (var vinculo in vinculos)
            vinculo.IsCover = vinculo.Id == mediaId;

        await _db.SaveChangesAsync();

        await _audit.RecordStrictAsync(
            userId: null,
            AuditAction.Update,
            EntityTechniqueMedia,
            link.Id,
            changes: new Dictionary<string, object?>
            {
                ["Actor"] = actor,
                ["TechniqueId"] = tecnicaId,
                ["MediaAssetId"] = link.MediaAssetId,
                ["IsCover"] = true,
                ["PortadasAnteriores"] = portadasAnteriores
            });

        await transaccion.CommitAsync();
        return ResultadoOperacion.Ok(tecnicaId);
    }

    // ── Desvincular (sin borrar el recurso) ────────────────────────────────

    public async Task<ResultadoOperacion> DesvincularAsync(int tecnicaId, int mediaId, string actor)
    {
        await using var transaccion = await _db.Database.BeginTransactionAsync();

        var link = await _db.TechniqueMedia
            .Include(tm => tm.MediaAsset)
            .FirstOrDefaultAsync(tm => tm.Id == mediaId && tm.TechniqueId == tecnicaId);

        if (link is null)
            return ResultadoOperacion.NoEncontrada();

        var assetId = link.MediaAssetId;

        // SOLO se elimina la asociación: el MediaAsset (archivo o recurso
        // compartido) no se marca borrado ni se purga.
        _db.TechniqueMedia.Remove(link);
        await _db.SaveChangesAsync();

        await _audit.RecordStrictAsync(
            userId: null,
            AuditAction.Purge,
            EntityTechniqueMedia,
            mediaId,
            changes: new Dictionary<string, object?>
            {
                ["Actor"] = actor,
                ["TechniqueId"] = tecnicaId,
                ["MediaAssetId"] = assetId,
                ["Desvinculado"] = true,
                ["MediaAssetConservado"] = true
            });

        await transaccion.CommitAsync();
        return ResultadoOperacion.Ok(tecnicaId);
    }

    // ── Validaciones ───────────────────────────────────────────────────────

    /// <summary>
    /// Validaciones de servidor: longitudes del dominio, formato de URL por
    /// parser, existencia de la técnica, límite de recursos y duplicados
    /// dentro de la misma técnica.
    /// </summary>
    private async Task<List<string>> ValidarAsync(
        MediaComando comando,
        int tecnicaId,
        int? mediaIdExcluido,
        bool urlObligatoria)
    {
        var errores = new List<string>();

        var url = comando.Url?.Trim();

        if (string.IsNullOrEmpty(url))
        {
            if (urlObligatoria)
                errores.Add("La URL del video es obligatoria.");
        }
        else if (url.Length > MaxUrlLength)
        {
            errores.Add($"La URL no puede superar los {MaxUrlLength} caracteres.");
        }
        else
        {
            var parsed = _parser.ParseUrl(url);
            if (!parsed.IsValid)
                errores.Add(parsed.ErrorMessage ?? "La URL no corresponde a un video soportado (YouTube o Vimeo).");
        }

        if (comando.Titulo?.Length > MaxTituloLength)
            errores.Add($"El título no puede superar los {MaxTituloLength} caracteres.");

        if (comando.Descripcion?.Length > MaxDescripcionLength)
            errores.Add($"La descripción no puede superar los {MaxDescripcionLength} caracteres.");

        if (errores.Count > 0)
            return errores;

        // Límite de recursos (solo al agregar).
        if (mediaIdExcluido is null)
        {
            var cantidad = await _db.TechniqueMedia.CountAsync(tm => tm.TechniqueId == tecnicaId);
            if (cantidad >= MaxRecursosPorTecnica)
                errores.Add(
                    $"Esta técnica ya alcanzó el límite de {MaxRecursosPorTecnica} recursos multimedia. " +
                    "Quitá uno antes de agregar otro.");
        }

        // Duplicados por (proveedor, videoId) o URL exacta en ESTA técnica.
        if (!string.IsNullOrEmpty(url) && errores.Count == 0)
        {
            var parsed = _parser.ParseUrl(url);
            var consulta = _db.TechniqueMedia
                .Where(tm => tm.TechniqueId == tecnicaId && tm.MediaAsset != null && !tm.MediaAsset.IsDeleted);

            if (mediaIdExcluido.HasValue)
                consulta = consulta.Where(tm => tm.Id != mediaIdExcluido.Value);

            var duplicado = await consulta
                .Select(tm => tm.MediaAsset)
                .AnyAsync(a =>
                    a.ExternalUrl == url ||
                    (a.ExternalProvider == parsed.Provider && a.ExternalVideoId == parsed.VideoId));

            if (duplicado)
                errores.Add("Ese video ya está asociado a esta técnica.");
        }

        return errores;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Usuario de dominio autor del recurso: primero por email del actor
    /// (sesión Identity con perfil de dominio), después el perfil con rol
    /// Administrador. Null = no hay usuario apto (el alta se rechaza).
    /// </summary>
    private async Task<int?> ResolverUsuarioAutorAsync(string actor)
    {
        if (!string.IsNullOrWhiteSpace(actor) && actor.Contains('@', StringComparison.Ordinal))
        {
            var porEmail = await _db.Users
                .AsNoTracking()
                .Where(u => u.Email == actor)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync();

            if (porEmail.HasValue)
                return porEmail;
        }

        return await _db.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Administrador)
            .OrderBy(u => u.Id)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>True si el recurso lo usan OTROS vínculos (otra técnica, perfil o recurso personal).</summary>
    private async Task<bool> EstaCompartidoAsync(int assetId, int tecnicaId, int mediaId)
    {
        var enOtraTecnica = await _db.TechniqueMedia.AnyAsync(tm =>
            tm.MediaAssetId == assetId && tm.Id != mediaId && tm.TechniqueId != tecnicaId);

        if (enOtraTecnica)
            return true;

        if (await _db.UserProfileMedia.AnyAsync(p => p.MediaAssetId == assetId))
            return true;

        if (await _db.UserTechniqueResources.AnyAsync(r => r.MediaAssetId == assetId))
            return true;

        // Mismo técnico: otro vínculo de ESTA técnica que apunte al mismo asset
        // (duplicado defensivo) también exige clonar para no afectarlo.
        return await _db.TechniqueMedia.AnyAsync(tm =>
            tm.MediaAssetId == assetId && tm.Id != mediaId && tm.TechniqueId == tecnicaId);
    }

    /// <summary>Copia profunda de un <see cref="MediaAsset"/> (nueva identidad, sin tocar el original).</summary>
    private static MediaAsset ClonarAsset(MediaAsset original, int? autorId) => new()
    {
        UploadedByUserId = autorId ?? original.UploadedByUserId,
        MediaType = original.MediaType,
        SourceType = original.SourceType,
        LocalPath = original.LocalPath,
        RemoteUrl = original.RemoteUrl,
        ExternalUrl = original.ExternalUrl,
        ExternalProvider = original.ExternalProvider,
        ExternalVideoId = original.ExternalVideoId,
        OriginalFileName = original.OriginalFileName,
        MimeType = original.MimeType,
        FileSizeBytes = original.FileSizeBytes,
        Title = original.Title,
        Description = original.Description,
        ThumbnailPath = original.ThumbnailPath,
        SyncStatus = original.SyncStatus,
        CreatedAt = DateTime.UtcNow,
        IsDeleted = false
    };

    private static string TituloPorDefecto(ParsedExternalMedia parsed) =>
        parsed.Provider == ExternalMediaProvider.Vimeo ? "Video de Vimeo" : "Video de YouTube";
}
