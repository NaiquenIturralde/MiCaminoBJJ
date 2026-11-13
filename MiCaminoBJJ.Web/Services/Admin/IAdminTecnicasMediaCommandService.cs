using MiCaminoBJJ.Web.ViewModels;

namespace MiCaminoBJJ.Web.Services.Admin;

/// <summary>Entrada de un comando multimedia administrativo (NO es una entidad EF).</summary>
public sealed record MediaComando(
    string? Url,
    string? Titulo,
    string? Descripcion);

/// <summary>
/// Comandos de escritura de la sección «Multimedia de la técnica» (Web sobre
/// <c>WebTfDbContext</c>), accesibles SOLO desde acciones administrativas
/// protegidas con la política <c>AdministradorContenido</c> y antiforgery.
///
/// Reglas de dominio:
/// - La URL se valida con <c>IExternalMediaParser</c> (YouTube watch/shorts/
///   youtu.be/embed y Vimeo); NUNCA se descarga el video al servidor.
/// - Alta: crea <c>MediaAsset</c> (ExternalLink) + vínculo <c>TechniqueMedia</c>
///   en una sola transacción auditada; el primer recurso queda como portada.
/// - Edición: actualiza título/descripción/URL del recurso (re-parseando la
///   URL cuando cambia) con copia sobre escritura si el recurso está compartido
///   con OTRA técnica o perfil (nunca se rompe a terceros).
/// - Portada: un solo recurso portada por técnica (se limpian las demás).
/// - Desvincular: elimina SOLO la asociación; el <c>MediaAsset</c> jamás se
///   elimina ni se marca borrado automáticamente (recurso compartido intacto).
/// - Auditoría estricta: operación + auditoría se confirman o revierten juntas.
/// </summary>
public interface IAdminTecnicasMediaCommandService
{
    /// <summary>Agrega un enlace de video a la técnica. 404 lógico si la técnica no existe.</summary>
    Task<ResultadoOperacion> AgregarAsync(int tecnicaId, MediaComando comando, string actor);

    /// <summary>Edita título, descripción y URL de un recurso YA asociado a la técnica.</summary>
    Task<ResultadoOperacion> EditarAsync(int tecnicaId, int mediaId, MediaComando comando, string actor);

    /// <summary>Marca un recurso como portada (única) de la técnica.</summary>
    Task<ResultadoOperacion> MarcarPortadaAsync(int tecnicaId, int mediaId, string actor);

    /// <summary>
    /// Quita la asociación recurso↔técnica tras confirmación. NO elimina el
    /// <c>MediaAsset</c> (archivo o recurso compartido queda intacto).
    /// </summary>
    Task<ResultadoOperacion> DesvincularAsync(int tecnicaId, int mediaId, string actor);
}
