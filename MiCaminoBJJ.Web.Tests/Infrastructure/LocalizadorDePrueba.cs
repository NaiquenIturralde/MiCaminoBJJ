using System.Globalization;
using MiCaminoBJJ.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace MiCaminoBJJ.Web.Tests.Infrastructure;

/// <summary>
/// Helper para obtener el localizador real de la web en las pruebas.
///
/// Usa EXACTAMENTE el mismo registro que la aplicación
/// (<c>AddLocalization</c> con <c>ResourcesPath = "Resources"</c>), de modo que
/// las pruebas resuelven los mismos archivos .resx que el runtime (es neutral,
/// en y pt) y no un doble que oculte un fallo de configuración.
/// </summary>
public static class LocalizadorDePrueba
{
    /// <summary>
    /// Localizador real de la web para la cultura indicada (sin cambiar la
    /// cultura actual: úsalo dentro de <see cref="ConCultura{T}"/>).
    /// </summary>
    public static IStringLocalizer<UiResources> Crear(string cultura = "es")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        using var proveedor = services.BuildServiceProvider();

        return proveedor.GetRequiredService<IStringLocalizer<UiResources>>();
    }

    /// <summary>
    /// Resuelve UNA clave en la cultura indicada y devuelve el texto.
    /// Es la forma recomendada de usarla en las pruebas.
    /// </summary>
    public static string Resolver(string cultura, string clave) =>
        ConCultura(cultura, () => (string)Crear(cultura)[clave]);

    /// <summary>Resuelve una clave con argumentos de formato.</summary>
    public static string Resolver(string cultura, string clave, params object[] argumentos) =>
        ConCultura(cultura, () => (string)Crear(cultura)[clave, argumentos]);

    /// <summary>
    /// Catálogo de idiomas con los nombres ya traducidos a la cultura indicada.
    /// </summary>
    public static IReadOnlyList<IdiomaOpcion> Opciones(string cultura) =>
        ConCultura(cultura, () => Idiomas.Opciones(Crear(cultura)));

    /// <summary>
    /// Ejecuta la acción con la cultura indicada y RESTAURA la original al
    /// terminar (incluye ante excepción).
    /// </summary>
    public static T ConCultura<T>(string cultura, Func<T> accion)
    {
        var originalUi = CultureInfo.CurrentUICulture;
        var original = CultureInfo.CurrentCulture;

        try
        {
            var nueva = new CultureInfo(cultura);
            CultureInfo.CurrentUICulture = nueva;
            CultureInfo.CurrentCulture = nueva;
            return accion();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUi;
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>Igual que <see cref="ConCultura{T}"/> pero para acciones async.</summary>
    public static async Task<T> ConCulturaAsync<T>(string cultura, Func<Task<T>> accion)
    {
        var originalUi = CultureInfo.CurrentUICulture;
        var original = CultureInfo.CurrentCulture;

        try
        {
            var nueva = new CultureInfo(cultura);
            CultureInfo.CurrentUICulture = nueva;
            CultureInfo.CurrentCulture = nueva;
            return await accion();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUi;
            CultureInfo.CurrentCulture = original;
        }
    }
}
