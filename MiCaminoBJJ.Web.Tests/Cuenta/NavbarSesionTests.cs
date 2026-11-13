using FluentAssertions;

namespace MiCaminoBJJ.Web.Tests.Cuenta;

/// <summary>
/// Etapa 4C — navegación condicional y control de contraseña (fuente Razor/JS):
/// guards de regresión sobre el markup real de las vistas compartidas y de
/// cuenta (la verificación en vivo de la navbar se hace por HTTP en la
/// validación final de la etapa).
/// </summary>
public class NavbarSesionTests
{
    private static string LeerArchivoWeb(params string[] partesRelativas)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var raizProyecto = Path.Combine(dir.FullName, "MiCaminoBJJ.Web");
            if (Directory.Exists(raizProyecto))
            {
                var destino = Path.Combine(new[] { raizProyecto }.Concat(partesRelativas).ToArray());
                if (File.Exists(destino))
                    return File.ReadAllText(destino);
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException("No se encontró " + string.Join("/", partesRelativas) + " desde " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Layout_TieneLosEnlacesDeSesionDePractico()
    {
        var layout = LeerArchivoWeb("Views", "Shared", "_Layout.cshtml");

        layout.Should().Contain("asp-controller=\"Cuenta\"", "enlace de cuenta en la navbar");
        layout.Should().Contain("asp-action=\"Login\"");
        layout.Should().Contain("asp-action=\"Registro\"");
        layout.Should().Contain("asp-action=\"Logout\"", "cierre de sesión por POST desde la navbar");
        layout.Should().Contain("asp-action=\"Perfil\"", "enlace a Mi perfil en la rama autenticada");
        layout.Should().Contain("IdentitySetup.AuthSchemeName",
            "la navbar distingue la sesión de práctico (esquema Identity.Application) de la sesión admin de Development (esquema \"Cookies\")");
        layout.Should().Contain("mccj-avatar-btn",
            "el avatar con menú reemplaza los botones superiores (Etapa 1)");
        layout.Should().Contain("aria-expanded=\"false\"",
            "el menú desplegable nace cerrado con su estado accesible");
        layout.Should().Contain("asp-controller=\"Ayuda\"",
            "la ayuda es una opción real del menú (enlace al manual)");
        layout.Should().NotContain("Próximamente",
            "ya no hay opciones deshabilitadas en el menú: la ayuda existe");
    }

    [Fact]
    public void VistasDeCuenta_IncluyenElControlDeMostrarPassword()
    {
        foreach (var vista in new[] { new[] { "Views", "Cuenta", "Registro.cshtml" }, new[] { "Views", "Cuenta", "Login.cshtml" } })
        {
            var contenido = LeerArchivoWeb(vista);

            contenido.Should().Contain("data-password-toggle",
                $"{vista[^1]} debe alternar la visibilidad de la contraseña");
            contenido.Should().Contain("password-toggle.js");
            contenido.Should().Contain("asp-validation-summary",
                $"{vista[^1]} debe mostrar validaciones de servidor");
            contenido.Should().Contain("asp-action=\"Registro\"".Replace("Registro", vista[^1].Replace(".cshtml", "")),
                $"{vista[^1]} publica en su propia acción");
        }
    }

    [Fact]
    public void Registro_NoExponeRoles_NiEnVistaNiEnViewModel()
    {
        var vista = LeerArchivoWeb("Views", "Cuenta", "Registro.cshtml");

        vista.Should().NotContain("Role", "la vista de registro no ofrece selección de rol");
        vista.Should().NotContain("Administrador");
    }

    [Fact]
    public void JavaScriptDePassword_NoUsaAlmacenamientoNiEnvios()
    {
        var js = LeerArchivoWeb("wwwroot", "js", "password-toggle.js");

        js.Should().NotContain("localStorage", "no se almacena nada del lado del cliente");
        js.Should().NotContain("sessionStorage");
        js.Should().NotContain("fetch(", "no se envía nada al servidor");
        js.Should().NotContain("XMLHttpRequest");
        js.Should().Contain("aria-pressed");
        js.Should().Contain("aria-label");
    }
}
