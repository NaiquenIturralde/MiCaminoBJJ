using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Web.ViewModels.Admin;

/// <summary>Listado administrativo de técnicas (incluye desactivadas).</summary>
public sealed class AdminTecnicasIndexViewModel
{
    public IReadOnlyList<AdminTecnicaFilaViewModel> Tecnicas { get; set; } =
        Array.Empty<AdminTecnicaFilaViewModel>();

    public string? Buscar { get; set; }

    /// <summary>Estado: true = solo activas, false = solo desactivadas, null = todas.</summary>
    public bool? Estado { get; set; }

    /// <summary>
    /// Cinturón seleccionado (Id del catálogo existente); null = todos.
    /// Tercer filtro independiente, combinable con la búsqueda y el estado.
    /// </summary>
    public int? Cinturon { get; set; }

    /// <summary>
    /// Catálogo de cinturones (Blanco, Azul, Violeta, Marrón, Negro) que
    /// alimenta el desplegable del filtro; se lee de la base, no está hardcodeado.
    /// </summary>
    public IReadOnlyList<FiltroOpcionViewModel> Cinturones { get; set; } =
        Array.Empty<FiltroOpcionViewModel>();

    public int TotalResultados => Tecnicas.Count;
    public bool HayResultados => Tecnicas.Count > 0;

    /// <summary>True cuando hay al menos un filtro activo (nombre, cinturón o estado).</summary>
    public bool TieneFiltros =>
        !string.IsNullOrWhiteSpace(Buscar) || Estado.HasValue || Cinturon.HasValue;

    /// <summary>Nombre del cinturón filtrado (null si no hay filtro de cinturón).</summary>
    public string? NombreCinturon =>
        Cinturon.HasValue ? Cinturones.FirstOrDefault(c => c.Id == Cinturon)?.Nombre : null;

    /// <summary>
    /// Valor textual del estado para los campos ocultos de los POST del
    /// listado ("", "true" o "false"): se evita pasar un <c>bool?</c> directo
    /// al atributo para que Razor no lo trate como atributo booleano.
    /// </summary>
    public string EstadoTexto => Estado switch
    {
        true => "true",
        false => "false",
        null => string.Empty
    };

    /// <summary>Query string de los filtros activos (vacío si no hay ninguno).</summary>
    public string QueryString => AdminTecnicasFiltros.QueryString(Buscar, Estado, Cinturon);

    /// <summary>Ruta del listado con los filtros activos (para volver sin perderlos).</summary>
    public string RutaListado => AdminTecnicasFiltros.RutaListado(Buscar, Estado, Cinturon);

    /// <summary>Ruta de edición de una técnica conservando los filtros del listado.</summary>
    public string RutaEdicion(int id) =>
        $"{AdminTecnicasFiltros.RutaBase}/Edit/{id}{QueryString}";
}

/// <summary>Fila administrativa de una técnica.</summary>
public sealed class AdminTecnicaFilaViewModel
{
    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Cinturon { get; init; } = string.Empty;
    public string Categoria { get; init; } = string.Empty;
    public string Posicion { get; init; } = string.Empty;
    public bool Activa { get; init; }
    public DateTime FechaUtc { get; init; }
    public bool FueEditada { get; init; }

    public static AdminTecnicaFilaViewModel Desde(Technique technique) => new()
    {
        Id = technique.Id,
        Nombre = technique.Name,
        Cinturon = technique.Belt?.Name ?? string.Empty,
        Categoria = technique.Category?.Name ?? string.Empty,
        Posicion = technique.Position?.Name ?? string.Empty,
        Activa = technique.IsActive,
        FechaUtc = technique.UpdatedAt ?? technique.CreatedAt,
        FueEditada = technique.UpdatedAt.HasValue
    };
}
