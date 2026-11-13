using MiCaminoBJJ.Web.Services.Guia;

namespace MiCaminoBJJ.Web.ViewModels.Guia;

/// <summary>
/// Respuesta JSON del Guía del Practicante hacia el navegador. El modo es una
/// ETIQUETA HONESTA: "catalogo" (guía basada en el catálogo de SQL Server) o
/// "ia" (respuesta generada por el proveedor de IA configurado).
/// </summary>
public sealed class GuiaRespuestaViewModel
{
    /// <summary>Modo de la respuesta: "catalogo" | "ia".</summary>
    public const string ModoCatalogo = "catalogo";
    public const string ModoIa = "ia";

    public string Texto { get; set; } = string.Empty;

    public string Modo { get; set; } = ModoCatalogo;

    public List<string> Sugerencias { get; set; } = new();

    /// <summary>true cuando no se pudo procesar (mensaje de error para el usuario).</summary>
    public bool EsError { get; set; }

    /// <summary>Nota visible (p. ej. fallback por fallo del proveedor de IA).</summary>
    public string? Nota { get; set; }

    public static GuiaRespuestaViewModel Desde(GuiaRespuesta respuesta) => new()
    {
        Texto = respuesta.Texto,
        Modo = respuesta.Modo == GuiaModo.InteligenciaArtificial ? ModoIa : ModoCatalogo,
        Sugerencias = respuesta.Sugerencias.ToList(),
        EsError = respuesta.EsError,
        Nota = respuesta.Nota
    };

    public static GuiaRespuestaViewModel Error(string mensaje) => new()
    {
        Texto = mensaje,
        Modo = ModoCatalogo,
        EsError = true
    };
}
