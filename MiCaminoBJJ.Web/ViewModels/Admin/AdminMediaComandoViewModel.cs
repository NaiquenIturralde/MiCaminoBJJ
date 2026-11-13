using System.ComponentModel.DataAnnotations;

namespace MiCaminoBJJ.Web.ViewModels.Admin;

/// <summary>
/// Formulario de ALTA de un recurso multimedia en la sección de Editar.
///
/// Los límites son los REALES del dominio (<c>MediaAssetConfiguration</c>:
/// ExternalUrl 2000, Title 300, Description 1000); la validación de formato
/// (YouTube/Vimeo) ocurre en el servidor con <c>IExternalMediaParser</c>.
/// </summary>
public sealed class AdminMediaAltaViewModel
{
    [Required(ErrorMessage = "La URL del video es obligatoria.")]
    [StringLength(2000, ErrorMessage = "La URL no puede superar los 2000 caracteres.")]
    public string Url { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "El título no puede superar los 300 caracteres.")]
    public string? Titulo { get; set; }

    [StringLength(1000, ErrorMessage = "La descripción no puede superar los 1000 caracteres.")]
    public string? Descripcion { get; set; }
}

/// <summary>
/// Formulario de EDICIÓN de un recurso ya asociado (una tarjeta por recurso).
///
/// <see cref="MediaId"/> NO lleva validación: llega de la RUTA (el controlador
/// lo normaliza desde allí y el servicio valida que el recurso pertenezca a la
/// técnica). <see cref="Url"/> es OPCIONAL en edición: vacío = conservar el
/// enlace actual (permite renombrar sin reescribir la URL y edita también
/// archivos locales sin forzarles un enlace). Título y descripción vacíos
/// limpian el campo (la ficha pública muestra la URL como respaldo).
/// </summary>
public sealed class AdminMediaEdicionViewModel
{
    /// <summary>Id de la asociación (<c>TechniqueMedia.Id</c>); fuente de verdad = ruta.</summary>
    public int MediaId { get; set; }

    [StringLength(2000, ErrorMessage = "La URL no puede superar los 2000 caracteres.")]
    public string? Url { get; set; }

    [StringLength(300, ErrorMessage = "El título no puede superar los 300 caracteres.")]
    public string? Titulo { get; set; }

    [StringLength(1000, ErrorMessage = "La descripción no puede superar los 1000 caracteres.")]
    public string? Descripcion { get; set; }
}
