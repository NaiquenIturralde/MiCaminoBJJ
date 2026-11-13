using System.ComponentModel.DataAnnotations;

namespace MiCaminoBJJ.Web.ViewModels.Admin;

/// <summary>Credencial del acceso administrativo local (Etapa 3).</summary>
public sealed class AdminLoginViewModel
{
    [Required(ErrorMessage = "Ingresá la contraseña de desarrollo.")]
    [DataType(DataType.Password)]
    [StringLength(256, ErrorMessage = "La contraseña no puede superar los 256 caracteres.")]
    public string Password { get; set; } = string.Empty;

    /// <summary>URL de retorno (solo rutas locales; se valida en el servidor).</summary>
    public string? ReturnUrl { get; set; }
}
