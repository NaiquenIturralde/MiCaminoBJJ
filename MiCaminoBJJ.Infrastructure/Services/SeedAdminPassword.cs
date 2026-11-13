using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace MiCaminoBJJ.Infrastructure.Services;

/// <summary>
/// Fuente única de la contraseña del administrador que crea el seed local de la
/// app móvil (base SQLite del dispositivo).
///
/// SEGURIDAD: el repositorio NO publica ninguna credencial fija.
/// La contraseña se resuelve, en este orden:
///   1. Configuración <c>AdminDev:Password</c> (User Secrets, variable de
///      entorno <c>AdminDev__Password</c> o <c>appsettings.json</c> local): es
///      la MISMA clave que ya usa el panel administrativo de la web.
///   2. Si no hay ninguna configurada, se genera una contraseña ALEATORIA con
///      un generador criptográfico: la cuenta de seed queda creada, pero SIN
///      una credencial fija, universal y adivinable versionada en el código.
///
/// El valor en claro nunca se persiste ni se escribe en el log: se entrega
/// únicamente a <see cref="MiCaminoBJJ.Core.Interfaces.IPasswordHasher"/> para
/// derivar el hash PBKDF2 con salt propio.
/// </summary>
public static class SeedAdminPassword
{
    /// <summary>Clave de configuración de la contraseña del admin sembrado.</summary>
    public const string ConfigKey = "AdminDev:Password";

    /// <summary>Longitud de la contraseña aleatoria generada.</summary>
    private const int LongitudAleatoria = 24;

    /// <summary>
    /// Alfabeto sin caracteres ambiguos (se excluyen 0/O, 1/l/I) para que la
    /// contraseña sea transcribible a mano si alguna vez hace falta.
    /// </summary>
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";

    /// <summary>
    /// Resuelve la contraseña que se debe usar para el administrador sembrado.
    /// </summary>
    /// <param name="configuration">
    /// Configuración de la aplicación. Puede ser <c>null</c> (por ejemplo en
    /// pruebas que construyen el servicio directamente): en ese caso se genera
    /// una contraseña aleatoria.
    /// </param>
    /// <returns>La contraseña en claro, para passarla al hasher.</returns>
    public static string Resolve(IConfiguration? configuration)
    {
        var configurada = configuration?[ConfigKey];

        return string.IsNullOrWhiteSpace(configurada)
            ? GenerarAleatoria()
            : configurada;
    }

    /// <summary>
    /// Genera una contraseña aleatoria criptográficamente segura. El valor no
    /// se devuelve al usuario en ningún flujo: solo se usa para derivar el hash.
    /// </summary>
    private static string GenerarAleatoria()
    {
        var buffer = new char[LongitudAleatoria];

        for (var i = 0; i < buffer.Length; i++)
            buffer[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];

        return new string(buffer);
    }
}
