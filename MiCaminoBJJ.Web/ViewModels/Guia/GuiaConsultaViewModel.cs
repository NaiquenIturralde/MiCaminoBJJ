using System.ComponentModel.DataAnnotations;
using MiCaminoBJJ.Web.Services.Guia;

namespace MiCaminoBJJ.Web.ViewModels.Guia;

/// <summary>
/// Pregunta enviada al Guía del Practicante (POST JSON). Validación estricta
/// en el servidor: obligatoria y con longitud máxima acotada.
/// </summary>
public sealed class GuiaConsultaViewModel
{
    [Required(ErrorMessage = "Escribí una pregunta antes de enviarla.")]
    [StringLength(
        GuiaLimites.MaxCaracteresPregunta,
        MinimumLength = 1,
        ErrorMessage = "La pregunta no puede superar los {1} caracteres.")]
    public string Texto { get; set; } = string.Empty;
}
