namespace MiCaminoBJJ.Web.ViewModels;

/// <summary>
/// Recursos multimedia de una técnica que la web puede mostrar de forma válida:
/// enlaces externos con URL real. Los archivos locales alojados en la app móvil
/// no se muestran hasta que exista un mecanismo de servido de archivos en la web:
/// no se inventan enlaces.
///
/// Cuando el enlace es de YouTube (watch, youtu.be, shorts o embed) el recurso
/// además expone los datos del reproductor integrado de la card Multimedia
/// (embed, miniatura y enlace canónico "Abrir en YouTube").
/// </summary>
public sealed class TecnicaRecursoViewModel
{
    public string Titulo { get; init; } = string.Empty;

    /// <summary>Descripción del recurso (la aporta el administrador o la importación del PDF).</summary>
    public string? Descripcion { get; init; }

    /// <summary>Enlace del recurso: URL canónica de YouTube o el enlace externo original.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>URL de embed de YouTube; null cuando no es un video integrable.</summary>
    public string? EmbedUrl { get; init; }

    /// <summary>Miniatura servida por YouTube (i.ytimg.com); null si no hay embed.</summary>
    public string? MiniaturaUrl { get; init; }

    /// <summary>True cuando el recurso se muestra con el reproductor integrado.</summary>
    public bool EsVideoIntegrado => !string.IsNullOrEmpty(EmbedUrl);
}
