using MiCaminoBJJ.Web.ViewModels;

namespace MiCaminoBJJ.Web.Services;

/// <summary>
/// Contenido de presentación del landing de inicio (Etapa 0 Web).
/// Aislado del resto de la arquitectura: no toca la base de datos, no ejecuta
/// migraciones ni requiere Identity. Su implementación puede cambiar (p. ej.
/// venir de Configuration o de la base) sin tocar el controlador.
/// </summary>
public interface IHomeContentService
{
    HomeIndexViewModel GetLandingContent();
}
