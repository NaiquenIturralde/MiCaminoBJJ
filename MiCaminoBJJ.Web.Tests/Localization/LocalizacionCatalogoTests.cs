using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using MiCaminoBJJ.Web.Localization;
using MiCaminoBJJ.Web.Tests.Infrastructure;
using Microsoft.Extensions.Localization;

namespace MiCaminoBJJ.Web.Tests.Localization;

/// <summary>
/// Pruebas de la BASE DE LOCALIZACIÓN de la web (es / en / pt).
///
/// Verifican tres cosas independientes:
/// 1. El catálogo de recursos tiene EXACTAMENTE las mismas claves en los tres
///    idiomas (una clave faltante en un idioma = texto sin traducir).
/// 2. El localizador real de ASP.NET Core resuelve cada idioma con un texto
///    DISTINTO (y nunca devuelve la clave en crudo).
/// 3. La normalización de códigos de idioma acepta los tres soportados y
///    rechaza cualquier otro.
/// </summary>
public sealed class LocalizacionCatalogoTests
{
    private static string CarpetaRecursos
    {
        get
        {
            // Sube desde bin/Debug/net10.0 hasta la raíz del repositorio y
            // entra en el proyecto Web (misma técnica que el resto de pruebas).
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var proyectoWeb = Path.Combine(dir.FullName, "MiCaminoBJJ.Web");
                var recursos = Path.Combine(proyectoWeb, "Resources");

                if (Directory.Exists(proyectoWeb) && Directory.Exists(recursos))
                    return recursos;

                dir = dir.Parent;
            }

            throw new InvalidOperationException(
                "No se encontró la carpeta Resources del proyecto MiCaminoBJJ.Web desde " + AppContext.BaseDirectory);
        }
    }

    private static Dictionary<string, string> Leer(string nombreArchivo)
    {
        var ruta = Path.Combine(CarpetaRecursos, nombreArchivo);
        File.Exists(ruta).Should().BeTrue($"debe existir el recurso '{nombreArchivo}'");

        var documento = XDocument.Load(ruta);

        return documento
            .Root!.Elements("data")
            .ToDictionary(
                d => (string)d.Attribute("name")!,
                d => d.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);
    }

    // ── 1) Paridad de claves entre los tres idiomas ───────────────────────

    [Fact]
    public void LosTresIdiomasTienenExactamenteLasMismasClaves()
    {
        var es = Leer("Localization.UiResources.resx");
        var en = Leer("Localization.UiResources.en.resx");
        var pt = Leer("Localization.UiResources.pt.resx");

        es.Should().NotBeEmpty();
        en.Keys.Should().BeEquivalentTo(es.Keys, "el catálogo en inglés no puede tener claves distintas al español");
        pt.Keys.Should().BeEquivalentTo(es.Keys, "el catálogo en portugués no puede tener claves distintas al español");
    }

    [Fact]
    public void NingunaClaveQuedaSinTraducirEnNingunIdioma()
    {
        var es = Leer("Localization.UiResources.resx");
        var en = Leer("Localization.UiResources.en.resx");
        var pt = Leer("Localization.UiResources.pt.resx");

        foreach (var clave in es.Keys)
        {
            es[clave].Should().NotBeNullOrWhiteSpace($"'{clave}' no puede estar vacía en español");
            en[clave].Should().NotBeNullOrWhiteSpace($"'{clave}' no puede estar vacía en inglés");
            pt[clave].Should().NotBeNullOrWhiteSpace($"'{clave}' no puede estar vacía en portugués");

            // Un valor idéntico a la clave significa "recurso no encontrado".
            en[clave].Should().NotBe(clave, $"'{clave}' no se resolvió en inglés");
            pt[clave].Should().NotBe(clave, $"'{clave}' no se resolvió en portugués");
        }
    }

    [Fact]
    public void LosTextosDeInterfazDifierenEntreLosTresIdiomas()
    {
        var es = Leer("Localization.UiResources.resx");
        var en = Leer("Localization.UiResources.en.resx");
        var pt = Leer("Localization.UiResources.pt.resx");

        // Claves de navegación y formularios: deben TRADUCIRSE de verdad.
        // Se eligen claves cuyo texto cambia realmente entre los tres idiomas.
        string[] claves = [
            "Layout.Nav.Inicio",
            "Layout.Nav.Guia",
            "Layout.Nav.MiPerfil",
            "Layout.Nav.CerrarSesion",
            "Cuenta.Login.Password",
            "Cuenta.Registro.Titulo",
            "Cuenta.Configuracion.IdiomaSeccion",
            "Cuenta.Configuracion.Guardar",
            "Validacion.Email.Requerido"
        ];

        foreach (var clave in claves)
        {
            en[clave].Should().NotBe(es[clave], $"'{clave}' debe estar traducido al inglés");
            pt[clave].Should().NotBe(es[clave], $"'{clave}' debe estar traducido al portugués");
        }
    }

    // ── 2) El localizador real de ASP.NET Core resuelve los tres idiomas ──

    [Fact]
    public void ElLocalizadorRealResuelveCadaIdiomaConSuTexto()
    {
        var textoEs = LocalizadorDePrueba.Resolver("es", "Layout.Nav.Inicio");
        var textoEn = LocalizadorDePrueba.Resolver("en", "Layout.Nav.Inicio");
        var textoPt = LocalizadorDePrueba.Resolver("pt", "Layout.Nav.Inicio");

        textoEs.Should().Be("Inicio");
        textoEn.Should().Be("Home");
        textoPt.Should().Be("Início");
    }

    [Fact]
    public void ElLocalizadorRealNuncaDevuelveLaClaveEnCrudo()
    {
        var claves = Leer("Localization.UiResources.resx").Keys.Take(40);

        foreach (var clave in claves)
        {
            var valor = LocalizadorDePrueba.Resolver("es", clave);
            valor.Should().NotBe(clave, $"'{clave}' no se resolvió desde el catálogo de recursos");
            valor.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void ElLocalizadorUsaElArgumentoDeFormato()
    {
        LocalizadorDePrueba.Resolver("es", "Layout.Nav.Rol", "Practicante").Should().Be("Rol: Practicante");
        LocalizadorDePrueba.Resolver("en", "Layout.Nav.Rol", "Practicante").Should().Be("Role: Practicante");
        LocalizadorDePrueba.Resolver("pt", "Layout.Nav.Rol", "Practicante").Should().Be("Papel: Practicante");
    }

    // ── 3) Normalización de códigos de idioma ─────────────────────────────

    [Theory]
    [InlineData("es", "es")]
    [InlineData("en", "en")]
    [InlineData("pt", "pt")]
    [InlineData("ES", "es")]
    [InlineData(" en ", "en")]
    [InlineData("es-ES", "es")]
    [InlineData("pt-BR", "pt")]
    [InlineData("en_US", "en")]
    public void Normalizar_AceptaLosIdiomasSoportados(string entrada, string esperado)
    {
        Idiomas.Normalizar(entrada).Should().Be(esperado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("zz-ZZ")]
    [InlineData("javascript")]
    public void Normalizar_RechazaIdiomasNoSoportados(string? entrada)
    {
        Idiomas.Normalizar(entrada).Should().BeNull();
        Idiomas.EsValido(entrada).Should().BeFalse();
    }

    [Fact]
    public void LaWebSoportaExactamenteLosTresIdiomas()
    {
        Idiomas.Codigos.Should().BeEquivalentTo(["es", "en", "pt"]);
        Idiomas.Culturas.Select(c => c.TwoLetterISOLanguageName)
            .Should().BeEquivalentTo(["es", "en", "pt"]);
    }

    [Fact]
    public void ElNombreDeLosIdiomasSeTraduceAlIdiomaActual()
    {
        var opcionesEn = LocalizadorDePrueba.Opciones("en");
        var opcionesPt = LocalizadorDePrueba.Opciones("pt");

        opcionesEn.Should().HaveCount(3);
        opcionesEn.Select(o => o.Codigo).Should().BeEquivalentTo(["es", "en", "pt"]);

        opcionesEn.First(o => o.Codigo == "es").Nombre.Should().Be("Spanish");
        opcionesEn.First(o => o.Codigo == "en").Nombre.Should().Be("English");
        opcionesEn.First(o => o.Codigo == "pt").Nombre.Should().Be("Portuguese");

        opcionesPt.First(o => o.Codigo == "es").Nombre.Should().Be("Espanhol");
        opcionesPt.First(o => o.Codigo == "en").Nombre.Should().Be("Inglês");
        opcionesPt.First(o => o.Codigo == "pt").Nombre.Should().Be("Português");
    }

    // ── 4) Toda clave usada en el código EXISTE en el catálogo ─────────────

    /// <summary>
    /// Recorre las vistas y los archivos .cs del proyecto Web buscando las
    /// claves del catálogo y verifica que TODAS existan.
    ///
    /// Sin este control, una clave mal escrita se renderiza tal cual
    /// ("Cuenta.Configuracion.MiPerfil" en pantalla) y el error pasa
    /// desapercibido: el catálogo es coherente entre idiomas, pero incompleto.
    /// </summary>
    [Fact]
    public void TodaClaveUsadaEnLasVistasExisteEnElCatalogo()
    {
        var claves = Leer("Localization.UiResources.resx").Keys.ToHashSet(StringComparer.Ordinal);
        var raiz = RaizProyectoWeb();

        var usadas = new HashSet<string>(StringComparer.Ordinal);

        foreach (var archivo in Directory.EnumerateFiles(Path.Combine(raiz, "Views"), "*.cshtml", SearchOption.AllDirectories))
        {
            var texto = File.ReadAllText(archivo);

            // @L["Clave"] y @L["Clave", arg]
            foreach (Match m in Regex.Matches(texto, @"@L\[\s*""([^""]+)"""))
                usadas.Add(m.Groups[1].Value);

            // ViewData["Title"] = L["Clave"]
            foreach (Match m in Regex.Matches(texto, @"L\[\s*""([^""]+)"""))
                usadas.Add(m.Groups[1].Value);
        }

        foreach (var archivo in Directory.EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories))
        {
            var nombre = Path.GetFileName(archivo);
            if (nombre is "obj" or "bin" || archivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || archivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            var texto = File.ReadAllText(archivo);

            foreach (Match m in Regex.Matches(texto, @"_localizador\[\s*""([^""]+)""\]"))
                usadas.Add(m.Groups[1].Value);

            foreach (Match m in Regex.Matches(texto, @"localizador\[\s*""([^""]+)""\]"))
                usadas.Add(m.Groups[1].Value);
        }

        usadas.Should().NotBeEmpty("debe haber claves usadas en el código");

        var faltantes = usadas.Where(c => !claves.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();

        faltantes.Should().BeEmpty(
            "estas claves se usan en el código pero NO existen en el catálogo (se mostrarían en crudo): "
            + string.Join(", ", faltantes));
    }

    /// <summary>Raíz del proyecto MiCaminoBJJ.Web (padre de la carpeta Resources).</summary>
    private static string RaizProyectoWeb()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var proyectoWeb = Path.Combine(dir.FullName, "MiCaminoBJJ.Web");
            if (Directory.Exists(proyectoWeb) && Directory.Exists(Path.Combine(proyectoWeb, "Resources")))
                return proyectoWeb;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("No se encontró la raíz del proyecto MiCaminoBJJ.Web.");
    }

    // ── 5) Los recursos estáticos NO consultan la preferencia ─────────────

    /// <summary>
    /// El middleware de idioma solo consulta la base para páginas HTML: los
    /// archivos estáticos (css, js, imágenes, fuentes) se sirven sin tocar la
    /// base, para no sumar una consulta por cada archivo descargado.
    /// </summary>
    [Theory]
    [InlineData("/css/site.css")]
    [InlineData("/js/site.js")]
    [InlineData("/img/LogoMiCaminoBJJ.png")]
    [InlineData("/images/belts/blanco.png")]
    [InlineData("/lib/bootstrap/dist/css/bootstrap.min.css")]
    [InlineData("/css/site.css?v=123")]
    public void LosRecursosEstaticosSeReconocen(string ruta)
    {
        RecursosEstaticos.EsEstatico(ruta).Should().BeTrue();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Cuenta/Configuracion")]
    [InlineData("/Tecnicas/Details/423")]
    [InlineData("/Ayuda")]
    [InlineData("")]
    [InlineData(null)]
    public void LasPaginasNoSeConfundenConRecursosEstaticos(string? ruta)
    {
        RecursosEstaticos.EsEstatico(ruta).Should().BeFalse();
    }
}
