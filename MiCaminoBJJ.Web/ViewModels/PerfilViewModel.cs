using System.ComponentModel.DataAnnotations;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Web.ViewModels;

/// <summary>
/// Modelo de la pantalla «Mi perfil» (/Cuenta/Perfil).
///
/// Es a la vez RESUMEN y FORMULARIO de la única pantalla de perfil:
/// - Editables: <see cref="Nombre"/> y <see cref="Cinturon"/> (el cinturón
///   solo puede tomar valores del catálogo real: ver
///   <see cref="CinturonVisual.OpcionesRegistro"/>).
/// - Solo lectura: email (es la clave de vinculación con Identity), rol (lo
///   asigna el servidor) y fecha de registro.
/// - La fotografía se muestra en 120×120 con esquinas redondeadas; si el
///   practicante no tiene foto se dibuja un avatar con sus iniciales.
///
/// El cinturón es un DATO DE PERFIL (personalización y filtros): NUNCA
/// concede permisos ni cambia el rol.
/// </summary>
public sealed class PerfilViewModel
{
    [Required(ErrorMessage = "Validacion.Nombre.Requerido")]
    [StringLength(100, ErrorMessage = "Validacion.Nombre.Largo")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validacion.Cinturon.Requerido")]
    public string Cinturon { get; set; } = string.Empty;

    /// <summary>Email de la cuenta (solo lectura).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Rol visible del practicante (solo lectura).</summary>
    public string Rol { get; set; } = string.Empty;

    /// <summary>Fecha de registro del perfil (solo lectura).</summary>
    public DateTime MiembroDesde { get; set; }

    /// <summary>URL de la foto de perfil vigente; null si no hay foto (avatar de iniciales).</summary>
    public string? FotoUrl { get; set; }

    /// <summary>Iniciales para el avatar de respaldo.</summary>
    public string Iniciales { get; set; } = string.Empty;

    /// <summary>Ruta virtual de la imagen PNG del cinturón seleccionado.</summary>
    public string? CinturonImagen { get; set; }

    /// <summary>Clase CSS del glow del color del cinturón.</summary>
    public string? CinturonClase { get; set; }

    /// <summary>Catálogo de cinturones (valores válidos del formulario).</summary>
    public IReadOnlyList<CinturonVisual.Opcion> OpcionesCinturon { get; set; } = CinturonVisual.OpcionesRegistro;

    /// <summary>
    /// Construye el modelo desde el perfil del dominio. <paramref name="fotoUrl"/>
    /// es la URL de la foto ya almacenada (null si no hay).
    /// </summary>
    public static PerfilViewModel Desde(User usuario, string? fotoUrl = null)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var visual = CinturonVisual.Para(usuario.BeltRank);
        var cinturon = visual?.Valor
            ?? (string.IsNullOrWhiteSpace(usuario.BeltRank) ? string.Empty : usuario.BeltRank.Trim());

        return new PerfilViewModel
        {
            Nombre = usuario.Name,
            Email = usuario.Email,
            Cinturon = cinturon,
            CinturonImagen = visual?.Imagen,
            CinturonClase = visual?.Clase,
            Rol = usuario.Role.ToString(),
            MiembroDesde = usuario.CreatedAt,
            FotoUrl = fotoUrl,
            Iniciales = InicialesDe(usuario.Name)
        };
    }

    /// <summary>
    /// Iniciales del avatar de respaldo: hasta dos letras del nombre (o del
    /// email si no hay nombre).
    /// </summary>
    public static string InicialesDe(string? nombre)
    {
        var baseNombre = (nombre ?? string.Empty).Trim();
        if (baseNombre.Length == 0)
            return "?";

        var arroba = baseNombre.IndexOf('@');
        if (arroba >= 0)
            baseNombre = baseNombre[..arroba];

        var palabras = baseNombre.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (palabras.Length == 0)
            return "?";

        var iniciales = palabras.Length >= 2
            ? $"{palabras[0][0]}{palabras[1][0]}"
            : palabras[0][0].ToString();

        return iniciales.ToUpperInvariant();
    }
}
