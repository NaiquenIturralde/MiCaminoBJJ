using System.Globalization;

namespace MiCaminoBJJ.Web.ViewModels.Admin;

/// <summary>
/// Parámetros GET del listado administrativo de técnicas
/// (/AdminTecnicas?buscar=..&amp;estado=..&amp;cinturon=..).
///
/// Centraliza la construcción del query string para que los tres criterios
/// (nombre/sinónimo, cinturón y estado) se conserven al navegar a Crear,
/// Editar, Desactivar o Reactivar y volver al listado: el mismo criterio que
/// usa <see cref="TecnicasFiltroViewModel"/> en la biblioteca pública.
/// </summary>
public static class AdminTecnicasFiltros
{
    public const string ParamBuscar = "buscar";
    public const string ParamEstado = "estado";
    public const string ParamCinturon = "cinturon";

    /// <summary>Ruta base del listado administrativo.</summary>
    public const string RutaBase = "/AdminTecnicas";

    /// <summary>
    /// Query string de los filtros aplicados ("?buscar=x&amp;cinturon=3") o
    /// cadena vacía cuando no hay ningún filtro activo.
    /// </summary>
    public static string QueryString(string? buscar, bool? estado, int? cinturon)
    {
        var partes = new List<string>(3);

        if (!string.IsNullOrWhiteSpace(buscar))
            partes.Add($"{ParamBuscar}={Uri.EscapeDataString(buscar.Trim())}");
        if (estado.HasValue)
            partes.Add($"{ParamEstado}={(estado.Value ? "true" : "false")}");
        if (cinturon.HasValue)
            partes.Add($"{ParamCinturon}={cinturon.Value.ToString(CultureInfo.InvariantCulture)}");

        return partes.Count == 0 ? string.Empty : "?" + string.Join("&", partes);
    }

    /// <summary>Ruta del listado conservando los filtros indicados.</summary>
    public static string RutaListado(string? buscar, bool? estado, int? cinturon) =>
        RutaBase + QueryString(buscar, estado, cinturon);
}
