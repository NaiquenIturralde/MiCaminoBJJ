using Microsoft.AspNetCore.Http;

namespace MiCaminoBJJ.Web.Services.Perfil;

/// <summary>Resultado de intentar guardar la fotografía de perfil.</summary>
/// <param name="Exito">true si la foto quedó almacenada y vinculada.</param>
/// <param name="Error">Mensaje para la persona cuando falló.</param>
/// <param name="MediaAssetId">Id del recurso multimedia creado (null si falló).</param>
public sealed record ResultadoFotoPerfil(bool Exito, string? Error = null, int? MediaAssetId = null);

/// <summary>Datos de la foto de perfil almacenada (sin contenido).</summary>
/// <param name="MediaAssetId">Id del recurso multimedia.</param>
/// <param name="RutaRelativa">Ruta relativa dentro de la carpeta de almacenamiento.</param>
/// <param name="MimeType">Tipo real (detectado al subir, no declarado).</param>
/// <param name="TamanoBytes">Tamaño del archivo en disco.</param>
public sealed record FotoPerfilInfo(int MediaAssetId, string RutaRelativa, string MimeType, long TamanoBytes);

/// <summary>
/// Servicio de la fotografía de perfil del practicante.
///
/// Persistencia REUTILIZANDO el modelo existente (sin tablas nuevas y sin
/// migraciones): la foto se guarda como <c>MediaAsset</c>
/// (<c>MediaType.Image</c>, <c>SourceType.LocalFile</c>) y se vincula con
/// <c>UserProfileMedia</c> (<c>IsProfilePicture = true</c>), exactamente igual
/// que el resto de la multimedia del dominio.
///
/// Aislamiento: cada archivo vive en la carpeta del usuario
/// (<c>{raíz}/{perfilId}/</c>) y el servicio SOLO devuelve el archivo
/// solicitado al dueño de la foto.
/// </summary>
public interface IPerfilFotoService
{
    /// <summary>
    /// Valida y guarda la foto, reemplazando la anterior (si la había).
    /// No toca ningún otro usuario ni ningún otro recurso.
    /// </summary>
    Task<ResultadoFotoPerfil> GuardarAsync(int perfilId, IFormFile? archivo, CancellationToken cancelacion = default);

    /// <summary>Foto de perfil vigente del practicante (null si no tiene).</summary>
    Task<FotoPerfilInfo?> ObtenerFotoAsync(int perfilId, CancellationToken cancelacion = default);

    /// <summary>
    /// Foto de perfil vigente del practicante para un recurso concreto
    /// (null si ese recurso no es su foto de perfil vigente).
    /// </summary>
    Task<FotoPerfilInfo?> ObtenerFotoPorRecursoAsync(int mediaAssetId, int perfilId, CancellationToken cancelacion = default);

    /// <summary>
    /// Ruta absoluta del archivo dentro de la carpeta de almacenamiento.
    /// Devuelve null si la ruta_relative se escapa de la carpeta (protección
    /// contra path traversal).
    /// </summary>
    string? ResolverRutaAbsoluta(string? rutaRelativa);

    /// <summary>Ruta pública de la foto (acción del controlador con control de acceso).</summary>
    static string RutaPublica(int mediaAssetId) => $"/Cuenta/FotoPerfil/{mediaAssetId}";
}
