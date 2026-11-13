using System.ComponentModel.DataAnnotations;

namespace MiCaminoBJJ.Web.ViewModels;

/// <summary>
/// Formulario de inicio de sesión de practicantes (Etapa 4C).
///
/// Los <c>ErrorMessage</c> son CLAVES del catálogo de recursos compartido
/// (Localization.UiResources): el localizador de DataAnnotations las traduce
/// al idioma de la petición. Ver AddDataAnnotationsLocalization en Program.cs.
/// </summary>
public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Validacion.Email.Requerido")]
    [EmailAddress(ErrorMessage = "Validacion.Email.Formato")]
    [StringLength(200, ErrorMessage = "Validacion.Email.Largo")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validacion.Password.Requerido")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    /// <summary>URL de retorno (solo rutas locales; se valida en el servidor).</summary>
    public string? ReturnUrl { get; set; }
}
