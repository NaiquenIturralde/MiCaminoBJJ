using System.Security.Claims;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Security;
using MiCaminoBJJ.Web.Services.Perfil;
using Microsoft.EntityFrameworkCore;

namespace MiCaminoBJJ.Web.ViewModels;

/// <summary>
/// Etapa 1 — datos del encabezado para la sesión de práctico autenticada
/// (saludo, rol, foto de perfil y visibilidad del acceso administrativo).
///
/// Fuentes de datos (SIN valores fijos):
/// - Nombre: el perfil de dominio vinculado a la identidad autenticada
///   (Users.Email == Identity.Name); si aún no existe perfil, se usa el
///   nombre de la propia identidad (email).
/// - Rol: los ROLES de la identidad autenticada (claims de rol que emite
///   Identity al iniciar sesión). El rol numérico del perfil se usa SOLO
///   como etiqueta visible de último recurso, JAMÁS para autorizar.
/// - Foto: la imagen de perfil ya existente en UserProfileMedia (solo URLs
///   http/https absolutas); no hay carga de fotografías en esta etapa.
/// </summary>
public sealed class EncabezadoUsuarioViewModel
{
    /// <summary>Nombre completo para el saludo de escritorio.</summary>
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Nombre abreviado (solo el primer nombre o el email sin dominio) para el saludo móvil.</summary>
    public string NombreCorto { get; init; } = string.Empty;

    /// <summary>Etiqueta visible del rol: Practicante / Mestre / Administrador.</summary>
    public string Rol { get; init; } = string.Empty;

    /// <summary>URL http(s) de la fotografía de perfil; null si no existe (avatar predeterminado).</summary>
    public string? FotoUrl { get; init; }

    /// <summary>
    /// true SOLO cuando la identidad autenticada tiene el rol de
    /// administrador (claim de rol). Es la ÚNICA condición que muestra el
    /// acceso al panel: nunca se mira el rol numérico del perfil.
    /// </summary>
    public bool EsAdministrador { get; init; }

    /// <summary>
    /// Construye los datos del encabezado a partir de la identidad
    /// autenticada (claims) y su perfil de dominio (solo lectura).
    /// </summary>
    public static async Task<EncabezadoUsuarioViewModel> DesdeAsync(
        ClaimsPrincipal principal,
        WebTfDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(db);

        var cuenta = principal.Identity?.Name?.Trim() ?? string.Empty;

        var perfil = cuenta.Length == 0
            ? null
            : await db.Users.AsNoTracking()
                .Where(u => u.Email == cuenta)
                .Select(u => new
                {
                    Nombre = u.Name,
                    Rol = u.Role,
                    Fotos = u.ProfileMedia
                        .Where(m => m.IsProfilePicture && !m.MediaAsset.IsDeleted)
                        .OrderByDescending(m => m.DisplayOrder)
                        .ThenByDescending(m => m.CreatedAt)
                        .Select(m => new FotoPerfil
                        {
                            MediaAssetId = m.MediaAssetId,
                            Remota = m.MediaAsset.RemoteUrl ?? m.MediaAsset.ExternalUrl,
                            Local = m.MediaAsset.LocalPath
                        })
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancellationToken);

        var nombre = string.IsNullOrWhiteSpace(perfil?.Nombre) ? cuenta : perfil.Nombre.Trim();

        return new EncabezadoUsuarioViewModel
        {
            Nombre = nombre,
            NombreCorto = Acortar(nombre),
            Rol = RolDeIdentidad(principal) ?? perfil?.Rol.ToString() ?? IdentitySetup.RolPracticante,
            FotoUrl = ResolverFoto(perfil?.Fotos),
            EsAdministrador = principal.IsInRole(UserRole.Administrador.ToString())
        };
    }

    /// <summary>
    /// URL de la foto del avatar: URL absoluta http(s) si el recurso la tiene
    /// (recurso externo) o la ruta de la acción con control de acceso cuando la
    /// foto es un archivo local subido por el propio practicante.
    /// </summary>
    private static string? ResolverFoto(FotoPerfil? foto)
    {
        if (foto is null)
            return null;

        var publica = FotoPublica(foto.Remota);
        if (publica is not null)
            return publica;

        if (!string.IsNullOrWhiteSpace(foto.Local))
            return IPerfilFotoService.RutaPublica(foto.MediaAssetId);

        return null;
    }

    /// <summary>Proyección de la foto de perfil (solo lectura).</summary>
    private sealed class FotoPerfil
    {
        public int MediaAssetId { get; init; }

        public string? Remota { get; init; }

        public string? Local { get; init; }
    }

    /// <summary>
    /// Rol visible tomado EXCLUSIVAMENTE de los claims de rol de la
    /// identidad autenticada (Practicante / Mestre / Administrador).
    /// </summary>
    private static string? RolDeIdentidad(ClaimsPrincipal principal)
    {
        foreach (var rol in new[] { UserRole.Administrador, UserRole.Mestre, UserRole.Practicante })
        {
            var etiqueta = rol.ToString();
            if (principal.IsInRole(etiqueta))
                return etiqueta;
        }

        return null;
    }

    /// <summary>Primer nombre para el saludo abreviado (o el email sin dominio).</summary>
    private static string Acortar(string nombre)
    {
        var baseNombre = nombre;
        var arroba = baseNombre.IndexOf('@');
        if (arroba >= 0)
            baseNombre = baseNombre[..arroba];

        var palabras = baseNombre.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return palabras.Length > 0 ? palabras[0] : baseNombre;
    }

    /// <summary>
    /// Solo URLs absolutas http/https: cualquier otra referencia (ruta local
    /// de archivo, por ejemplo) se descarta para no generar imágenes rotas.
    /// </summary>
    private static string? FotoPublica(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        url = url.Trim();
        return url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? url
            : null;
    }
}
