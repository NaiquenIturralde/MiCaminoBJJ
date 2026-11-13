using System.Net.Http.Headers;
using System.Text.Json;

namespace MiCaminoBJJ.Web.Services.Guia;

/// <summary>
/// Cliente HTTP del proveedor de IA compatible con <c>POST {endpoint}</c>
/// estilo chat/completions (OpenAI-compatible).
///
/// Reglas de seguridad:
/// - La clave viaja SOLO en el header Authorization del request saliente.
/// - Nunca se registra en logs la clave, el endpoint ni el cuerpo (los
///   errores devueltos al servicio son genéricos, sin secretos).
/// - Timeout acotado (<see cref="GuiaLimites.TimeoutProveedor"/>): al excederse
///   se lanza <see cref="OperationCanceledException"/> y el guía responde con
///   el modo catálogo.
/// </summary>
public sealed class ProveedorIaCliente : IProveedorIaCliente
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly GuiaOpciones _opciones;

    /// <summary>
    /// El HttpClient lo inyecta el registro de CLIENTE TIPO
    /// (<c>AddHttpClient&lt;IProveedorIaCliente, ProveedorIaCliente&gt;</c>), que
    /// también aplica el timeout global configurado en Program.cs.
    /// </summary>
    public ProveedorIaCliente(HttpClient httpClient, GuiaOpciones opciones)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
    }

    public async Task<string> CompletarAsync(GuiaPeticionIa peticion, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        if (!_opciones.EstaConfigurada())
            throw new InvalidOperationException(
                "El proveedor de IA no está configurado (GuiaPracticante:ApiKey/Endpoint/Modelo).");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
        timeout.CancelAfter(GuiaLimites.TimeoutProveedor);

        using var mensaje = new HttpRequestMessage(HttpMethod.Post, _opciones.Endpoint);
        mensaje.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opciones.ApiKey);
        mensaje.Content = JsonContent.Create(new
        {
            model = _opciones.Modelo,
            temperature = 0.4,
            max_tokens = GuiaLimites.MaxTokensRespuesta,
            messages = new object[]
            {
                new { role = "system", content = peticion.Sistema },
                new { role = "user", content = peticion.Pregunta }
            }
        }, options: Json);

        using var respuesta = await _http.SendAsync(mensaje, timeout.Token);

        if (!respuesta.IsSuccessStatusCode)
        {
            // Sin cuerpo ni headers: el error no puede filtrar información del proveedor.
            throw new HttpRequestException(
                $"El proveedor de IA respondió HTTP {(int)respuesta.StatusCode}.");
        }

        await using var flujo = await respuesta.Content.ReadAsStreamAsync(cancelacion);
        using var documento = await JsonDocument.ParseAsync(flujo, cancellationToken: cancelacion);

        var contenido = documento.RootElement
            .TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array
            && choices.GetArrayLength() > 0
            && choices[0].TryGetProperty("message", out var mensajeIa)
            && mensajeIa.TryGetProperty("content", out var contenidoIa)
                ? contenidoIa.GetString()
                : null;

        if (string.IsNullOrWhiteSpace(contenido))
            throw new InvalidOperationException("El proveedor de IA no devolvió contenido.");

        return contenido!;
    }
}
