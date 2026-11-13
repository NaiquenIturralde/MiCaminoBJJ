namespace MiCaminoBJJ.Web.Services.Guia;

/// <summary>
/// Cliente del proveedor de IA (solo servidor: la clave NUNCA sale de
/// ASP.NET Core ni se expone en JavaScript, Razor, HTML, URLs ni logs).
///
/// Implementación: <see cref="ProveedorIaCliente"/> (HTTP con HttpClientFactory).
/// En pruebas se usa un doble falso: ninguna corrida paga llamadas reales.
/// </summary>
public interface IProveedorIaCliente
{
    /// <summary>
    /// Genera la respuesta del modelo. Lanza excepción (timeout, red, HTTP
    /// distinto de 2xx, respuesta malformada) — el servicio decide el fallback.
    /// </summary>
    Task<string> CompletarAsync(GuiaPeticionIa peticion, CancellationToken cancelacion);
}
