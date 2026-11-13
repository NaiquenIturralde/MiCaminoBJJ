using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;

namespace MiCaminoBJJ.Web.Services.Admin;

/// <summary>
/// Credencial de desarrollo local para el CRUD administrativo (Etapa 3).
///
/// Reglas de seguridad:
/// - Solo opera en Development (la validación recibe <c>esDevelopment</c>).
/// - La contraseña vive FUERA del repositorio (User Secrets / variables de
///   entorno), nunca en appsettings.json versionado.
/// - Validación en el SERVIDOR con comparación de tiempo fijo
///   (<see cref="CryptographicOperations.FixedTimeEquals"/>); ningún valor
///   viaja por el navegador para autorizar acciones (solo la cookie emitida
///   tras esta validación, firmada por ASP.NET Core).
/// </summary>
public static class DevAdminCredential
{
    /// <summary>Clave de configuración de la contraseña (User Secrets / env).</summary>
    public const string PasswordConfigKey = "AdminDev:Password";

    /// <summary>Clave de configuración opcional del nombre de usuario.</summary>
    public const string UserConfigKey = "AdminDev:User";

    /// <summary>Nombre por defecto del actor administrativo local.</summary>
    public const string DefaultUser = "admin-local";

    /// <summary>Claim de rol emitido en la cookie administrativa.</summary>
    public const string RoleClaim = "Administrador";

    /// <summary>
    /// Validación pura (testeable sin infraestructura): habilitado solo en
    /// Development y solo con contraseña esperada e ingresada no vacías.
    /// </summary>
    public static bool EsValida(string? passwordEsperada, string? passwordIngresada, bool esDevelopment)
    {
        if (!esDevelopment)
            return false;

        if (string.IsNullOrWhiteSpace(passwordEsperada))
            return false;

        if (string.IsNullOrEmpty(passwordIngresada))
            return false;

        // Tiempo fijo sobre bytes: no existe banda horaria por longitud aquí
        // (la contraseña es de desarrollo y el módulo solo existe en Development).
        var esperada = Encoding.UTF8.GetBytes(passwordEsperada);
        var ingresada = Encoding.UTF8.GetBytes(passwordIngresada);
        return CryptographicOperations.FixedTimeEquals(esperada, ingresada);
    }
}

/// <summary>Verifica la credencial de desarrollo y reporta si el módulo está habilitado.</summary>
public interface IDevAdminCredentialValidator
{
    /// <summary>True SOLO en Development con credencial configurada.</summary>
    bool EstaHabilitado { get; }

    /// <summary>Nombre del actor administrativo local (claim Name de la cookie).</summary>
    string Usuario { get; }

    /// <summary>Valida la contraseña ingresada contra la configuración del servidor.</summary>
    bool PasswordValido(string? password);
}

public sealed class DevAdminCredentialValidator : IDevAdminCredentialValidator
{
    private readonly string? _passwordEsperada;
    private readonly bool _esDevelopment;

    public DevAdminCredentialValidator(IConfiguration configuration, IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        _passwordEsperada = configuration[DevAdminCredential.PasswordConfigKey];
        _esDevelopment = environment.IsDevelopment();
        Usuario = configuration[DevAdminCredential.UserConfigKey] ?? DevAdminCredential.DefaultUser;
    }

    public bool EstaHabilitado => _esDevelopment && !string.IsNullOrWhiteSpace(_passwordEsperada);

    public string Usuario { get; }

    public bool PasswordValido(string? password) =>
        DevAdminCredential.EsValida(_passwordEsperada, password, _esDevelopment);
}
