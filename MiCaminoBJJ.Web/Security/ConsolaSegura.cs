using System.Text;

namespace MiCaminoBJJ.Web.Security;

/// <summary>
/// Lectura de contraseñas por consola SIN ECO (un '*' por tecla).
///
/// Extraído del patrón ya auditado de <see cref="AdminBootstrap"/> para que
/// ambos comandos de mantenimiento compartan UNA sola implementación (evita
/// divergencias: si un día se corrige el manejo de Backspace o de entrada
/// redirigida, se corrige en los dos a la vez).
///
/// Reglas:
/// - Si la entrada está redirigida (tuberías, CI, archivo) se RECHAZA: una
///   contraseña nunca debe poder inyectarse por stdin de un script.
/// - No se escribe la contraseña en ningún buffer persistente ni en logs.
/// </summary>
internal static class ConsolaSegura
{
    /// <summary>
    /// Lee una contraseña sin eco. Devuelve <c>null</c> si no hay consola
    /// interactiva (el llamador debe abortar la operación).
    /// </summary>
    public static string? LeerPassword()
    {
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("RECHAZADO: se requiere entrada interactiva (no hay consola).");
            return null;
        }

        var builder = new StringBuilder();
        while (true)
        {
            var tecla = Console.ReadKey(intercept: true);
            if (tecla.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (tecla.Key == ConsoleKey.Backspace)
            {
                if (builder.Length > 0)
                {
                    builder.Length--;
                    Console.Write("\b \b");
                }
                continue;
            }

            if (!char.IsControl(tecla.KeyChar))
            {
                builder.Append(tecla.KeyChar);
                Console.Write('*');
            }
        }

        return builder.ToString();
    }
}
