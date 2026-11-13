namespace MiCaminoBJJ.Web.Services.Guia;

/// <summary>
/// Límites y tiempos del Guía del Practicante (Etapa 5).
/// Están en un solo lugar para que la validación, el cliente HTTP y el
/// servicio usen SIEMPRE los mismos valores.
/// </summary>
public static class GuiaLimites
{
    /// <summary>Máximo de caracteres por pregunta del practicante.</summary>
    public const int MaxCaracteresPregunta = 1000;

    /// <summary>Máximo de caracteres de la respuesta entregada al navegador.</summary>
    public const int MaxCaracteresRespuesta = 4000;

    /// <summary>
    /// Tokens máximos pedidos al proveedor de IA (respuesta corta, alineada
    /// con la regla del prompt de ≤150 palabras). gemma3:1b en CPU rinde ~14
    /// tok/s: con 450 se excedía el timeout del proveedor.
    /// </summary>
    public const int MaxTokensRespuesta = 300;

    /// <summary>
    /// Timeout por llamada al proveedor. Si se excede, se responde con el modo
    /// catálogo (nunca se cuelga la petición del usuario). 45 s: cubre la
    /// carga en frío del modelo en Ollama más la generación en CPU.
    /// </summary>
    public static readonly TimeSpan TimeoutProveedor = TimeSpan.FromSeconds(45);

    /// <summary>Timeout duro del HttpClient (siempre mayor que el del proveedor).</summary>
    public static readonly TimeSpan TimeoutHttp = TimeSpan.FromSeconds(60);
}
