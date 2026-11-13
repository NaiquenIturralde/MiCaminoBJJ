using System.Text.Json;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;

namespace MiCaminoBJJ.Web.ViewModels;

/// <summary>
/// Detalle de una técnica. Solo expone datos existentes en el dominio:
/// nombre, descripción, objetivo, pasos/consejos (JSON de la entidad),
/// cinturón, categoría, posición, alias y multimedia válida.
/// Los bloques sin datos quedan vacíos para que la vista muestre un estado
/// vacío adecuado en lugar de inventar información.
/// </summary>
public sealed class TecnicaDetalleViewModel
{
    /// <summary>
    /// Parser compartido con el recorrido de cinturón blanco: acepta
    /// youtube.com/watch, youtu.be, youtube.com/shorts y youtube.com/embed.
    /// Stateless: una única instancia por tipo.
    /// </summary>
    private static readonly IExternalMediaParser ParserYouTube = new ExternalMediaParser();

    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public string Objetivo { get; init; } = string.Empty;
    public string Cinturon { get; init; } = string.Empty;
    public string Categoria { get; init; } = string.Empty;
    public string Posicion { get; init; } = string.Empty;

    public IReadOnlyList<string> Pasos { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Consejos { get; init; } = Array.Empty<string>();

    /// <summary>Sinónimos registrados (<see cref="TechniqueAlias.Name"/>).</summary>
    public IReadOnlyList<string> Alias { get; init; } = Array.Empty<string>();

    /// <summary>Recursos multimedia válidos (enlaces externos con URL real).</summary>
    public IReadOnlyList<TecnicaRecursoViewModel> Recursos { get; init; } = Array.Empty<TecnicaRecursoViewModel>();

    /// <summary>Subconjunto con reproductor integrado (YouTube) para la card Multimedia.</summary>
    public IReadOnlyList<TecnicaRecursoViewModel> Videos =>
        Recursos.Where(r => r.EsVideoIntegrado).ToList();

    /// <summary>Enlaces externos sin reproductor: se muestran como lista simple.</summary>
    public IReadOnlyList<TecnicaRecursoViewModel> EnlacesExternos =>
        Recursos.Where(r => !r.EsVideoIntegrado).ToList();

    /// <summary>True si la ficha puede mostrar el reproductor integrado.</summary>
    public bool TieneVideoIntegrado => Recursos.Any(r => r.EsVideoIntegrado);

    /// <summary>Ruta de vuelta al listado conservando los filtros aplicados.</summary>
    public string RutaVolver { get; init; } = "/Tecnicas";

    public bool EsFuenteDeDemostracion { get; init; }

    public bool TieneObjetivo => !string.IsNullOrWhiteSpace(Objetivo);

    public static TecnicaDetalleViewModel Desde(Technique technique, string rutaVolver, bool esFuenteDeDemostracion)
    {
        var recursos = new List<TecnicaRecursoViewModel>();

        foreach (var media in technique.OfficialMedia ?? Enumerable.Empty<TechniqueMedia>())
        {
            var asset = media.MediaAsset;
            if (asset is null || asset.IsDeleted)
                continue;

            // Solo lo que la web puede mostrar de verdad: enlaces externos válidos.
            if (asset.SourceType != MediaSourceType.ExternalLink)
                continue;

            var url = asset.ExternalUrl?.Trim();
            if (string.IsNullOrEmpty(url))
                continue;

            // YouTube (watch, youtu.be, shorts o embed) → reproductor integrado,
            // con el MISMO parser que usa el recorrido de cinturón blanco.
            var parsed = ParserYouTube.ParseUrl(url);
            var esYouTube = parsed.IsValid
                && parsed.Provider == ExternalMediaProvider.YouTube
                && !string.IsNullOrWhiteSpace(parsed.VideoId);

            recursos.Add(new TecnicaRecursoViewModel
            {
                Titulo = string.IsNullOrWhiteSpace(asset.Title) ? url : asset.Title!,
                Descripcion = asset.Description,
                // URL canónica para "Abrir en YouTube" (válida también para Shorts).
                Url = esYouTube ? $"https://www.youtube.com/watch?v={parsed.VideoId}" : url,
                EmbedUrl = esYouTube ? parsed.EmbedUrl : null,
                MiniaturaUrl = esYouTube ? $"https://i.ytimg.com/vi/{parsed.VideoId}/hqdefault.jpg" : null
            });
        }

        return new TecnicaDetalleViewModel
        {
            Id = technique.Id,
            Nombre = technique.Name,
            Descripcion = technique.Description,
            Objetivo = technique.Objective,
            Cinturon = technique.Belt?.Name ?? string.Empty,
            Categoria = technique.Category?.Name ?? string.Empty,
            Posicion = technique.Position?.Name ?? string.Empty,
            Pasos = ParsearLista(technique.Steps),
            Consejos = ParsearLista(technique.Tips),
            Alias = (technique.Aliases ?? new List<TechniqueAlias>())
                .Select(a => a.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .OrderBy(n => n, StringComparer.CurrentCulture)
                .ToList(),
            Recursos = recursos,
            RutaVolver = rutaVolver,
            EsFuenteDeDemostracion = esFuenteDeDemostracion
        };
    }

    /// <summary>
    /// Parsea los campos Steps/Tips (JSON array de strings de la entidad).
    /// Un dato corrupto o vacío produce una lista vacía: nunca rompe la vista.
    /// </summary>
    private static IReadOnlyList<string> ParsearLista(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)?
                       .Where(item => !string.IsNullOrWhiteSpace(item))
                       .ToList()
                   ?? new List<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
