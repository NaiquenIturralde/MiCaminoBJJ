namespace MiCaminoBJJ.Web.Services.Perfil;

/// <summary>
/// Opciones de la fotografía de perfil (sección de configuración
/// <c>PerfilFotos</c>).
///
/// - <see cref="Ruta"/>: carpeta donde se guardan los archivos. Si no se
///   define, la web usa <c>{ContentRoot}/App_Data/fotos-perfil</c>. NUNCA se
///   escribe dentro de <c>wwwroot</c>: las imágenes se sirven por una acción
///   del controlador (con control de acceso), no como archivo estático.
/// - <see cref="TamanoMaximoBytes"/>: tope de tamaño (2 MB por defecto).
///
/// La configuración NO habilita ninguna funcionalidad nueva por sí sola: solo
/// ajusta dónde y cuánto se almacena la foto que ya se puede subir desde
/// «Mi perfil».
/// </summary>
public sealed class PerfilFotoOpciones
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "PerfilFotos";

    /// <summary>Carpeta de almacenamiento (vacío = carpeta por defecto de la app).</summary>
    public string? Ruta { get; set; }

    /// <summary>Tope de tamaño del archivo (2 MB por defecto).</summary>
    public long TamanoMaximoBytes { get; set; } = PerfilFotoValidador.TamanoMaximoBytes;
}
