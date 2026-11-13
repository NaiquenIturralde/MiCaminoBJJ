using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;

namespace MiCaminoBJJ.Web.Services.Recorrido;

/// <summary>
/// Video de demostración vinculado a una ficha del recorrido (Etapa 6A).
///
/// La URL se valida SIEMPRE con <see cref="IExternalMediaParser"/> (parser
/// compartido con el resto de la app): si el enlace no corresponde a un
/// proveedor soportado, la construcción lanza y el catálogo completo no se
/// carga (falla temprana en desarrollo y en las pruebas, nunca en silencio).
///
/// No se descarga ni se duplica ningún video dentro del proyecto: solo se
/// guardan el ID validado y las URLs derivadas de él.
/// </summary>
public sealed class VideoRecorrido
{
    private VideoRecorrido(string urlOriginal, string titulo, string nota, string videoId, string embedUrl)
    {
        UrlOriginal = urlOriginal;
        Titulo = titulo;
        Nota = nota;
        VideoId = videoId;
        EmbedUrl = embedUrl;
    }

    /// <summary>URL original aportada por la usuaria (fuente única de verdad).</summary>
    public string UrlOriginal { get; }

    /// <summary>Título accesible del video (también usado como título del iframe).</summary>
    public string Titulo { get; }

    /// <summary>Nota de estado: la correspondencia visual pendiente de validación.</summary>
    public string Nota { get; }

    /// <summary>ID de YouTube validado (11 caracteres, [a-zA-Z0-9_-]).</summary>
    public string VideoId { get; }

    /// <summary>URL de embed derivada del ID validado.</summary>
    public string EmbedUrl { get; }

    /// <summary>URL para "abrir en YouTube" (funciona también con Shorts).</summary>
    public string WatchUrl => $"https://www.youtube.com/watch?v={VideoId}";

    /// <summary>Vista previa (miniatura) servida por YouTube; no se aloja en el proyecto.</summary>
    public string MiniaturaUrl => $"https://i.ytimg.com/vi/{VideoId}/hqdefault.jpg";

    /// <summary>
    /// Crea un video validando la URL con el parser compartido. Lanza si la URL
    /// no es válida o si el proveedor no es YouTube (los cuatro enlaces del
    /// recorrido son de YouTube / YouTube Shorts).
    /// </summary>
    public static VideoRecorrido Crear(string url, string titulo, string nota, IExternalMediaParser parser)
    {
        ArgumentNullException.ThrowIfNull(parser);

        var parsed = parser.ParseUrl(url);
        if (!parsed.IsValid || parsed.Provider != MiCaminoBJJ.Core.Enums.ExternalMediaProvider.YouTube)
        {
            throw new InvalidOperationException(
                $"La URL de video '{url}' no es un enlace de YouTube válido. {parsed.ErrorMessage}");
        }

        return new VideoRecorrido(url.Trim(), titulo, nota, parsed.VideoId!, parsed.EmbedUrl!);
    }
}

/// <summary>Punto teórico de un módulo educativo del recorrido.</summary>
public sealed record PuntoContenido(string Titulo, string Texto);

/// <summary>Estado de disponibilidad de video de un módulo.</summary>
public enum EstadoVideoModulo
{
    /// <summary>El módulo tiene al menos una ficha con video confirmado.</summary>
    ConVideo,

    /// <summary>Módulo teórico sin demostración confirmada: se muestra el aviso discreto.</summary>
    Pendiente
}

/// <summary>
/// Módulo educativo del recorrido cinturón blanco (cinco en total).
/// </summary>
public sealed record ModuloBlanco(
    int Id,
    string Slug,
    string Titulo,
    string Resumen,
    IReadOnlyList<PuntoContenido> Puntos,
    EstadoVideoModulo EstadoVideo,
    IReadOnlyList<FichaBlanco> Fichas,
    IReadOnlyList<string> FichasRelacionadas);

/// <summary>
/// Ficha técnica del recorrido. Es contenido EDUCATIVO del recorrido: no es ni
/// duplica una entidad <c>Technique</c>. Cuando existe una técnica equivalente
/// en la biblioteca se la referencia por nombre (<see cref="TecnicaRelacionada"/>)
/// para enlazarla sin atribuirle un cinturón principal nuevo.
/// </summary>
public sealed record FichaBlanco(
    string Slug,
    string Nombre,
    int ModuloId,
    string TipoContenido,
    string PosicionInicial,
    string Objetivo,
    string Introduccion,
    IReadOnlyList<string> Pasos,
    IReadOnlyList<string> Principios,
    IReadOnlyList<string> ErroresFrecuentes,
    IReadOnlyList<string> Precauciones,
    VideoRecorrido? Video,
    string? TecnicaRelacionada,
    string? CinturonPrincipal,
    string? ReferenciaBibliografica,
    string EstadoRevision,
    bool RequiereRevisionInstructor);
