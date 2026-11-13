using MiCaminoBJJ.Web.Localization;
using MiCaminoBJJ.Web.Services.Guia;
using Microsoft.Extensions.Localization;

namespace MiCaminoBJJ.Web.ViewModels.Guia;

/// <summary>Turno visible en la conversación.</summary>
public sealed class GuiaMensajeViewModel
{
    /// <summary>"practicante" | "agente".</summary>
    public string Rol { get; init; } = "agente";

    public string Texto { get; init; } = string.Empty;
}

/// <summary>
/// Estado inicial de la pantalla del Guía del Practicante.
/// </summary>
public sealed class GuiaChatViewModel
{
    public List<GuiaMensajeViewModel> Mensajes { get; init; } = new();

    public List<string> Sugerencias { get; init; } = new();

    /// <summary>Etiqueta del modo activo (honesta: catálogo vs IA).</summary>
    public string Modo { get; init; } = GuiaRespuestaViewModel.ModoCatalogo;

    /// <summary>
    /// Construye el estado inicial con los textos ya TRADUCIDOS al idioma
    /// de la petición (bienvenida y preguntas sugeridas).
    /// </summary>
    public static GuiaChatViewModel Inicial(string modo, IStringLocalizer<UiResources> localizador)
    {
        ArgumentNullException.ThrowIfNull(localizador);

        return new GuiaChatViewModel
        {
            Modo = modo,
            Sugerencias = GuiaPracticanteService.SugerenciasIniciales(localizador).ToList(),
            Mensajes = new List<GuiaMensajeViewModel>
            {
                new() { Rol = "agente", Texto = localizador["Guia.Bienvenida"] }
            }
        };
    }
}
