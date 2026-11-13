using System.ComponentModel.DataAnnotations;

namespace MiCaminoBJJ.Web.ViewModels;

/// <summary>
/// Formulario de registro de practicantes (Etapa 4C).
///
/// Campos: los mínimos reales del perfil de dominio (Email, Nombre y
/// Cinturón, que <c>Users</c> exige obligatorios) más las credenciales de
/// Identity. NO expone ningún campo de rol: el rol se asigna EXCLUSIVAMENTE en
/// el servidor (Practicante); no existe forma de elegirlo desde el cliente.
/// El cinturón es un DATO DE PERFIL (personalización y filtros), valida contra
/// una lista fija de valores del dominio y jamás otorga permisos.
/// </summary>
public sealed class RegistroViewModel
{
    [Required(ErrorMessage = "Validacion.Nombre.Requerido")]
    [StringLength(100, ErrorMessage = "Validacion.Nombre.Largo")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validacion.Email.Requerido")]
    [EmailAddress(ErrorMessage = "Validacion.Email.Formato")]
    [StringLength(200, ErrorMessage = "Validacion.Email.Largo")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Cinturón actual del practicante: obligatorio y restringido a los
    /// cinturones del dominio (Blanco, Azul, Violeta, Marrón, Negro).
    /// Required cubre el vacío (cliente y servidor) y RegularExpression
    /// rechaza cualquier valor fuera de la lista (solo evalúa valores no
    /// vacíos, por lo que no duplica el mensaje de Required).
    /// </summary>
    [Required(ErrorMessage = "Validacion.Cinturon.Requerido")]
    [RegularExpression("^(Blanco|Azul|Violeta|Marrón|Negro)$",
        ErrorMessage = "Validacion.Cinturon.Lista")]
    public string Cinturon { get; set; } = string.Empty;

    /// <summary>
    /// Réplica del lado cliente de la política de Identity (longitud ≥8,
    /// mayúscula, minúscula, número y carácter especial). El servidor la
    /// valida igualmente con el validador real de Identity.
    /// </summary>
    [Required(ErrorMessage = "Validacion.Password.Requerido")]
    [DataType(DataType.Password)]
    [StringLength(100, ErrorMessage = "Validacion.Password.Largo")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$",
        ErrorMessage = "Validacion.Password.Compleja")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validacion.ConfirmarPassword.Requerido")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Validacion.ConfirmarPassword.NoCoincide")]
    public string ConfirmarPassword { get; set; } = string.Empty;
}
