using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MiCaminoBJJ.Web.ViewModels.Admin;

/// <summary>
/// Formulario de alta/edición de técnicas (Etapa 3).
///
/// Nunca se expone la entidad EF en un POST: este ViewModel es el único
/// modelo de entrada y aplica DataAnnotations con los límites REALES del
/// dominio (TechniqueConfiguration: Name 200, Description 2000, Objective
/// 1000, Steps/Tips 4000 serializados). Los desplegables son obligatorios
/// (Range ≥ 1) y la existencia de la FK se valida en el servicio de comandos.
///
/// Pasos/Consejos se editan como texto (un ítem por línea) y se serializan a
/// JSON (mismo formato que usa la entidad Technique.Steps/Tips).
/// </summary>
public sealed class AdminTecnicaFormViewModel
{
    /// <summary>0 = creación; distinto de 0 = edición.</summary>
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(200, ErrorMessage = "El nombre no puede superar los 200 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(2000, ErrorMessage = "La descripción no puede superar los 2000 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    [Required(ErrorMessage = "El objetivo es obligatorio.")]
    [StringLength(1000, ErrorMessage = "El objetivo no puede superar los 1000 caracteres.")]
    public string Objetivo { get; set; } = string.Empty;

    /// <summary>Pasos, uno por línea (se serializa a JSON al guardar).</summary>
    [StringLength(4000, ErrorMessage = "Los pasos no pueden superar los 4000 caracteres.")]
    public string? Pasos { get; set; }

    /// <summary>Consejos, uno por línea (se serializa a JSON al guardar).</summary>
    [StringLength(4000, ErrorMessage = "Los consejos no pueden superar los 4000 caracteres.")]
    public string? Consejos { get; set; }

    [Required(ErrorMessage = "Seleccioná un cinturón.")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un cinturón.")]
    public int? CinturonId { get; set; }

    [Required(ErrorMessage = "Seleccioná una categoría.")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una categoría.")]
    public int? CategoriaId { get; set; }

    [Required(ErrorMessage = "Seleccioná una posición.")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una posición.")]
    public int? PosicionId { get; set; }

    public IReadOnlyList<FiltroOpcionViewModel> Cinturones { get; set; } = Array.Empty<FiltroOpcionViewModel>();
    public IReadOnlyList<FiltroOpcionViewModel> Categorias { get; set; } = Array.Empty<FiltroOpcionViewModel>();
    public IReadOnlyList<FiltroOpcionViewModel> Posiciones { get; set; } = Array.Empty<FiltroOpcionViewModel>();

    /// <summary>
    /// Sección «Multimedia de la técnica» (solo en Editar; null en Crear).
    ///
    /// Es SOLO de presentación: <c>[BindNever]</c> impide que un POST malicioso
    /// la enlace — la carga siempre el servidor en Edit GET y en el re-render
    /// tras un POST inválido. Los formularios de la sección publican en
    /// <c>AdminTecnicasMediaController</c>, no en Edit/Create.
    /// </summary>
    [BindNever]
    public AdminTecnicaMediaViewModel? Multimedia { get; set; }

    /// <summary>True cuando es edición (el form publica a Edit).</summary>
    public bool EsEdicion => Id > 0;

    // ── Conservación de los filtros del listado ───────────────────────────
    // Se reenvían como campos ocultos del formulario: al guardar o cancelar
    // se vuelve al listado CON los mismos criterios de búsqueda, cinturón y
    // estado con los que se entró (no se pierden al editar ni al crear).

    /// <summary>Filtro de búsqueda del listado del que se vino.</summary>
    public string? FiltroBuscar { get; set; }

    /// <summary>Filtro de estado del listado del que se vino (null = todas).</summary>
    public bool? FiltroEstado { get; set; }

    /// <summary>Filtro de cinturón del listado del que se vino (null = todos).</summary>
    public int? FiltroCinturon { get; set; }

    /// <summary>True si el formulario arrastra algún filtro del listado.</summary>
    public bool TieneFiltroListado =>
        !string.IsNullOrWhiteSpace(FiltroBuscar) || FiltroEstado.HasValue || FiltroCinturon.HasValue;

    /// <summary>Query string de los filtros arrastrados (vacío si no hay ninguno).</summary>
    public string QueryStringFiltro =>
        AdminTecnicasFiltros.QueryString(FiltroBuscar, FiltroEstado, FiltroCinturon);

    /// <summary>Ruta del listado con los filtros arrastrados (para "Cancelar").</summary>
    public string RutaListado =>
        AdminTecnicasFiltros.RutaListado(FiltroBuscar, FiltroEstado, FiltroCinturon);

    /// <summary>
    /// Valor textual del estado para el campo oculto del formulario
    /// ("", "true" o "false"): se evita pasar un <c>bool?</c> directo al
    /// atributo para que Razor no lo trate como atributo booleano.
    /// </summary>
    public string FiltroEstadoTexto => FiltroEstado switch
    {
        true => "true",
        false => "false",
        null => string.Empty
    };

    /// <summary>
    /// Texto (un ítem por línea) → JSON de lista, mismo formato que la entidad.
    /// Vacío o solo espacios → null (campo opcional en el dominio).
    /// </summary>
    public static string? AJson(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        var lineas = texto
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();

        return lineas.Length == 0 ? null : JsonSerializer.Serialize(lineas);
    }

    /// <summary>JSON de lista → texto (un ítem por línea) para editar.</summary>
    public static string? ATexto(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var lineas = JsonSerializer.Deserialize<string[]>(json);
            return lineas is null ? null : string.Join(Environment.NewLine, lineas);
        }
        catch (JsonException)
        {
            // Valor no parseable (dato heredado): se muestra tal cual para no perderlo.
            return json;
        }
    }
}
