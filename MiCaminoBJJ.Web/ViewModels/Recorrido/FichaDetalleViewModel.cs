using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Web.Services.Recorrido;

namespace MiCaminoBJJ.Web.ViewModels.Recorrido;

/// <summary>
/// Ficha técnica individual del recorrido blanco, con video integrado,
/// enlace a la técnica de la biblioteca (si existe) y navegación entre fichas.
/// </summary>
public sealed class FichaDetalleViewModel
{
    public string Slug { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string TipoContenido { get; init; } = string.Empty;

    public int ModuloId { get; init; }
    public string ModuloTitulo { get; init; } = string.Empty;
    public string ModuloSlug { get; init; } = string.Empty;

    public string PosicionInicial { get; init; } = string.Empty;
    public string Objetivo { get; init; } = string.Empty;
    public string Introduccion { get; init; } = string.Empty;

    public IReadOnlyList<string> Pasos { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Principios { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ErroresFrecuentes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Precauciones { get; init; } = Array.Empty<string>();

    public VideoDemostrativoViewModel? Video { get; init; }

    /// <summary>Cinturón principal mostrado (puede ser "Sin asignar").</summary>
    public string CinturonPrincipalTexto { get; init; } = "Sin asignar";

    public bool CinturonSinAsignar { get; init; }

    /// <summary>true cuando el cinturón principal NO es blanco: material complementario.</summary>
    public bool MaterialComplementario { get; init; }

    public bool TieneVideo => Video is not null;

    /// <summary>Nombre de la técnica existente de la biblioteca vinculada (null si no hay).</summary>
    public string? TecnicaRelacionadaNombre { get; init; }

    /// <summary>Ruta al detalle real de la técnica en la biblioteca (null si no se pudo resolver).</summary>
    public string? TecnicaRelacionadaUrl { get; init; }

    /// <summary>Cinturón principal REAL de la técnica en la biblioteca (null si no se resolvió).</summary>
    public string? TecnicaRelacionadaCinturon { get; init; }

    /// <summary>true si había técnica relacionada pero la biblioteca no respondió.</summary>
    public bool BibliotecaNoDisponible { get; init; }

    /// <summary>
    /// true si la técnica relacionada existe pero está OCULTA (IsActive = 0):
    /// se muestra su nombre y cinturón real sin enlazar a la ficha pública
    /// (que responde 404 para técnicas fuera de la biblioteca).
    /// </summary>
    public bool TecnicaRelacionadaOculta { get; init; }

    public string ReferenciaBibliografica { get; init; } = CinturonBlancoCatalogo.ReferenciaSinVerificar;
    public string EstadoRevision { get; init; } = CinturonBlancoCatalogo.EstadoRevisionFicha;
    public bool RequiereRevisionInstructor { get; init; }

    public string NotaMaterialComplementario { get; init; } = CinturonBlancoCatalogo.NotaMaterialComplementario;

    public string RutaIndice => "/recorrido/cinturon-blanco";
    public string RutaModulo => $"/recorrido/cinturon-blanco/modulo/{ModuloSlug}";

    public bool TieneAnterior { get; init; }
    public string? SlugAnterior { get; init; }
    public string? NombreAnterior { get; init; }

    public bool TieneSiguiente { get; init; }
    public string? SlugSiguiente { get; init; }
    public string? NombreSiguiente { get; init; }

    public string RutaAnterior => $"/recorrido/cinturon-blanco/ficha/{SlugAnterior}";
    public string RutaSiguiente => $"/recorrido/cinturon-blanco/ficha/{SlugSiguiente}";

    /// <summary>
    /// Construye la ficha desde el catálogo. La técnica relacionada se resuelve
    /// contra la biblioteca real: si se encuentra, se enlaza y se muestra SU
    /// cinturón principal; si no, se conserva el dato del catálogo y se marca
    /// que la biblioteca no respondió.
    /// </summary>
    public static FichaDetalleViewModel Desde(
        FichaBlanco ficha,
        ModuloBlanco modulo,
        FichaBlanco? anterior,
        FichaBlanco? siguiente,
        Technique? tecnicaRelacionada,
        bool bibliotecaNoDisponible)
    {
        var cinturonBiblioteca = tecnicaRelacionada?.Belt?.Name;

        return new FichaDetalleViewModel
        {
            Slug = ficha.Slug,
            Nombre = ficha.Nombre,
            TipoContenido = ficha.TipoContenido,
            ModuloId = modulo.Id,
            ModuloTitulo = modulo.Titulo,
            ModuloSlug = modulo.Slug,
            PosicionInicial = ficha.PosicionInicial,
            Objetivo = ficha.Objetivo,
            Introduccion = ficha.Introduccion,
            Pasos = ficha.Pasos,
            Principios = ficha.Principios,
            ErroresFrecuentes = ficha.ErroresFrecuentes,
            Precauciones = ficha.Precauciones,
            Video = ficha.Video is null ? null : VideoDemostrativoViewModel.Desde(ficha.Video),
            CinturonPrincipalTexto = cinturonBiblioteca ?? ficha.CinturonPrincipal ?? "Sin asignar",
            CinturonSinAsignar = cinturonBiblioteca is null && ficha.CinturonPrincipal is null,
            MaterialComplementario = (cinturonBiblioteca ?? ficha.CinturonPrincipal) is string cinturon
                                     && !string.Equals(cinturon, "Blanco", StringComparison.OrdinalIgnoreCase),
            TecnicaRelacionadaNombre = tecnicaRelacionada?.Name ?? ficha.TecnicaRelacionada,
            // Enlazar SOLO si está activa: una técnica oculta por la importación
            // resuelve (nombre/cinturón reales) pero su ficha pública da 404.
            TecnicaRelacionadaUrl = tecnicaRelacionada is not { IsActive: true }
                ? null
                : $"/Tecnicas/Details/{tecnicaRelacionada.Id}",
            TecnicaRelacionadaOculta = tecnicaRelacionada is { IsActive: false },
            TecnicaRelacionadaCinturon = cinturonBiblioteca,
            BibliotecaNoDisponible = tecnicaRelacionada is null
                                     && ficha.TecnicaRelacionada is not null
                                     && bibliotecaNoDisponible,
            ReferenciaBibliografica = ficha.ReferenciaBibliografica ?? CinturonBlancoCatalogo.ReferenciaSinVerificar,
            EstadoRevision = ficha.EstadoRevision,
            RequiereRevisionInstructor = ficha.RequiereRevisionInstructor,
            TieneAnterior = anterior is not null,
            SlugAnterior = anterior?.Slug,
            NombreAnterior = anterior?.Nombre,
            TieneSiguiente = siguiente is not null,
            SlugSiguiente = siguiente?.Slug,
            NombreSiguiente = siguiente?.Nombre
        };
    }
}
