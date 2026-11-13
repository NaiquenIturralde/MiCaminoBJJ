using MiCaminoBJJ.Web.ViewModels.Admin;

namespace MiCaminoBJJ.Web.Services.Admin;

/// <summary>
/// Consultas de solo lectura de la sección «Multimedia de la técnica»
/// (AsNoTracking, sin escrituras): lista los recursos asociados a una técnica
/// ya convertidos en su ViewModel (reproductor, miniatura 16:9 de YouTube y
/// Shorts, portada, orden).
/// </summary>
public interface IAdminTecnicasMediaQueryService
{
    /// <summary>
    /// Recursos de la técnica ordenados por portada y luego por orden visible;
    /// excluye recursos borrados lógicamente. Vacío si la técnica no existe.
    /// </summary>
    Task<IReadOnlyList<AdminMediaRecursoViewModel>> ObtenerMultimediaAsync(int tecnicaId);
}
