using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Web.Persistence;

namespace MiCaminoBJJ.Web.Services.Perfil;

/// <summary>
/// Implementación del servicio de fotografía de perfil sobre
/// <see cref="WebTfDbContext"/> (SQL Server de la web).
///
/// Garantías:
/// - El USUARIO affected se toma del perfil del solicitante: el servicio
///   nunca recibe ni usa el identificador de otro usuario.
/// - La validación del archivo ocurre ANTES de escribir en disco y ANTES de
///   tocar la base (tipo, tamaño y contenido real: ver
///   <see cref="PerfilFotoValidador"/>).
/// - El nombre del archivo lo genera el servidor (GUID): el nombre original
///   del usuario NUNCA se usa como ruta (evita path traversal y colisiones).
/// - Archivo + registro en base + vínculo <c>UserProfileMedia</c> se hacen en
///   una transacción; si la base falla, el archivo nuevo se borra (el
///   anterior queda intacto).
/// - Al reemplazar, la foto anterior se da de baja lógicamente (soft delete)
///   y su archivo se elimina del disco DESPUÉS del commit.
/// </summary>
public sealed class PerfilFotoService : IPerfilFotoService
{
    private readonly WebTfDbContext _db;
    private readonly ILogger<PerfilFotoService> _logger;
    private readonly PerfilFotoOpciones _opciones;

    public PerfilFotoService(
        WebTfDbContext db,
        IOptions<PerfilFotoOpciones> opciones,
        ILogger<PerfilFotoService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _opciones = opciones?.Value ?? new PerfilFotoOpciones();
    }

    /// <summary>Carpeta raíz de almacenamiento de las fotos de perfil.</summary>
    public string Raiz => string.IsNullOrWhiteSpace(_opciones.Ruta)
        ? Path.Combine(AppContext.BaseDirectory, "fotos-perfil")
        : Path.GetFullPath(_opciones.Ruta);

    /// <summary>Tope de tamaño efectivo (nunca mayor a 5 MB).</summary>
    private long TamanoMaximo => Math.Clamp(
        _opciones.TamanoMaximoBytes <= 0 ? PerfilFotoValidador.TamanoMaximoBytes : _opciones.TamanoMaximoBytes,
        1024,
        5L * 1024 * 1024);

    // ── Guardado ──────────────────────────────────────────────────────────

    public async Task<ResultadoFotoPerfil> GuardarAsync(
        int perfilId,
        IFormFile? archivo,
        CancellationToken cancelacion = default)
    {
        if (perfilId <= 0)
            return new ResultadoFotoPerfil(false, "No se encontró tu perfil de practicante.");

        // 1) Validación (tipo, tamaño y contenido real) ANTES de escribir nada.
        var validacion = PerfilFotoValidador.Validar(archivo);
        if (!validacion.EsValido)
            return new ResultadoFotoPerfil(false, validacion.Error);

        // 2) El perfil debe existir (FK obligatoria de MediaAsset.UploadedByUserId).
        if (!await _db.Users.AsNoTracking().AnyAsync(u => u.Id == perfilId, cancelacion))
            return new ResultadoFotoPerfil(false, "No se encontró tu perfil de practicante.");

        var carpeta = Path.Combine(Raiz, perfilId.ToString(CultureInfo.InvariantCulture));
        var nombreArchivo = Guid.NewGuid().ToString("N") + validacion.Extension;
        var rutaAbsoluta = Path.Combine(carpeta, nombreArchivo);
        var rutaRelativa = Path.Combine(
            perfilId.ToString(CultureInfo.InvariantCulture),
            nombreArchivo);

        // 3) Escritura del archivo con tope de tamaño (defensa adicional).
        try
        {
            Directory.CreateDirectory(carpeta);
            await using (var origen = archivo!.OpenReadStream())
            await using (var destino = new FileStream(
                             rutaAbsoluta, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                long total = 0;
                int leidos;
                while ((leidos = await origen.ReadAsync(buffer, cancelacion)) > 0)
                {
                    total += leidos;
                    if (total > TamanoMaximo)
                        throw new InvalidDataException("El archivo supera el tamaño máximo permitido.");

                    await destino.WriteAsync(buffer.AsMemory(0, leidos), cancelacion);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "No se pudo guardar la foto de perfil (PerfilId={PerfilId}).", perfilId);
            BorrarArchivo(rutaAbsoluta);
            return new ResultadoFotoPerfil(false, "No se pudo guardar la foto. Intentá nuevamente en unos minutos.");
        }

        // 4) Base de datos: recurso + vínculo. Transacción única.
        List<(string RutaRelativa, string RutaAbsoluta)> archivosAnteriores = new();
        try
        {
            await using var transaccion = await _db.Database.BeginTransactionAsync(cancelacion);

            var anteriores = await _db.UserProfileMedia
                .Where(pm => pm.UserId == perfilId)
                .Include(pm => pm.MediaAsset)
                .ToListAsync(cancelacion);

            var asset = new MediaAsset
            {
                UploadedByUserId = perfilId,
                MediaType = MediaType.Image,
                SourceType = MediaSourceType.LocalFile,
                LocalPath = rutaRelativa,
                ExternalProvider = ExternalMediaProvider.None,
                OriginalFileName = NombreOriginalSeguro(archivo!.FileName),
                MimeType = validacion.MimeType,
                FileSizeBytes = new FileInfo(rutaAbsoluta).Length,
                Title = "Foto de perfil",
                SyncStatus = SyncStatus.LocalOnly,
                CreatedAt = DateTime.UtcNow
            };

            _db.MediaAssets.Add(asset);
            await _db.SaveChangesAsync(cancelacion);

            // Se retiran los vínculos previos ANTES de insertar el nuevo
            // (índice único filtrado: un solo IsProfilePicture por usuario).
            if (anteriores.Count > 0)
            {
                _db.UserProfileMedia.RemoveRange(anteriores);
                await _db.SaveChangesAsync(cancelacion);

                foreach (var previa in anteriores)
                {
                    var medioAnterior = previa.MediaAsset;
                    if (medioAnterior is null)
                        continue;

                    // Baja lógica: la fila se conserva (auditoría) pero deja de
                    // mostrarse (las consultas filtran IsDeleted).
                    medioAnterior.IsDeleted = true;
                    medioAnterior.UpdatedAt = DateTime.UtcNow;

                    var rutaPrevia = ResolverRutaAbsoluta(medioAnterior.LocalPath);
                    if (rutaPrevia is not null)
                        archivosAnteriores.Add((medioAnterior.LocalPath!, rutaPrevia));
                }

                await _db.SaveChangesAsync(cancelacion);
            }

            _db.UserProfileMedia.Add(new UserProfileMedia
            {
                UserId = perfilId,
                MediaAssetId = asset.Id,
                IsProfilePicture = true,
                DisplayOrder = 0,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancelacion);
            await transaccion.CommitAsync(cancelacion);

            // 5) Limpieza del archivo anterior (después del commit).
            foreach (var anterior in archivosAnteriores)
                BorrarArchivo(anterior.RutaAbsoluta);

            return new ResultadoFotoPerfil(true, null, asset.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo vincular la foto de perfil (PerfilId={PerfilId}).", perfilId);
            // Compensación: el archivo nuevo no queda huérfano en disco.
            BorrarArchivo(rutaAbsoluta);
            return new ResultadoFotoPerfil(false, "No se pudo guardar la foto. Intentá nuevamente en unos minutos.");
        }
    }

    // ── Consultas ─────────────────────────────────────────────────────────

    public async Task<FotoPerfilInfo?> ObtenerFotoAsync(int perfilId, CancellationToken cancelacion = default)
    {
        return await ConsultarFotoAsync(
            _db.UserProfileMedia
                .Where(pm => pm.UserId == perfilId && pm.IsProfilePicture && !pm.MediaAsset.IsDeleted),
            cancelacion);
    }

    public async Task<FotoPerfilInfo?> ObtenerFotoPorRecursoAsync(
        int mediaAssetId,
        int perfilId,
        CancellationToken cancelacion = default)
    {
        return await ConsultarFotoAsync(
            _db.UserProfileMedia
                .Where(pm => pm.MediaAssetId == mediaAssetId
                             && pm.UserId == perfilId
                             && pm.IsProfilePicture
                             && !pm.MediaAsset.IsDeleted),
            cancelacion);
    }

    private async Task<FotoPerfilInfo?> ConsultarFotoAsync(
        IQueryable<UserProfileMedia> consulta,
        CancellationToken cancelacion)
    {
        var fila = await consulta
            .AsNoTracking()
            .OrderByDescending(pm => pm.CreatedAt)
            .Select(pm => new
            {
                pm.MediaAssetId,
                pm.MediaAsset.LocalPath,
                pm.MediaAsset.MimeType,
                pm.MediaAsset.FileSizeBytes
            })
            .FirstOrDefaultAsync(cancelacion);

        if (fila is null || string.IsNullOrWhiteSpace(fila.LocalPath))
            return null;

        return new FotoPerfilInfo(
            fila.MediaAssetId,
            fila.LocalPath,
            string.IsNullOrWhiteSpace(fila.MimeType) ? "image/jpeg" : fila.MimeType,
            fila.FileSizeBytes ?? 0);
    }

    // ── Utilidades ────────────────────────────────────────────────────────

    /// <summary>
    /// Convierte la ruta relativa en absoluta garantizando que SIGA dentro
    /// de la carpeta de almacenamiento (nunca se sirve un archivo de otro
    /// sitio del disco).
    /// </summary>
    public string? ResolverRutaAbsoluta(string? rutaRelativa)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa))
            return null;

        var raiz = Raiz;
        var completa = Path.GetFullPath(Path.Combine(raiz, rutaRelativa));

        var comparacion = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!completa.StartsWith(raiz, comparacion))
            return null;

        return completa;
    }

    /// <summary>Nombre original saneado (solo información; nunca se usa como ruta).</summary>
    private static string? NombreOriginalSeguro(string? nombreOriginal)
    {
        if (string.IsNullOrWhiteSpace(nombreOriginal))
            return null;

        var nombre = Path.GetFileName(nombreOriginal.Trim());
        if (string.IsNullOrWhiteSpace(nombre))
            return null;

        // Se eliminan caracteres de control y se limita la longitud.
        var saneado = new string(nombre.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (saneado.Length == 0)
            return null;

        return saneado.Length > 200 ? saneado[..200] : saneado;
    }

    /// <summary>Borrado del archivo en disco (silencioso: no afecta la operación ya confirmada).</summary>
    private void BorrarArchivo(string rutaAbsoluta)
    {
        try
        {
            if (File.Exists(rutaAbsoluta))
                File.Delete(rutaAbsoluta);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar el archivo anterior de la foto de perfil.");
        }
    }
}
