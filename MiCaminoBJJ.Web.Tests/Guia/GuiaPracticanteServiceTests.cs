using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Web.Services;
using MiCaminoBJJ.Web.Services.Guia;
using MiCaminoBJJ.Web.Tests.Infrastructure;

namespace MiCaminoBJJ.Web.Tests.Guia;

/// <summary>
/// Fase B — pruebas UNITARIAS del Guía del Practicante (sin SQL Server y SIN
/// llamadas reales a ningún proveedor de IA: se usa un doble falso).
///
/// La cultura se fija en español (la de las aserciones) y se restaura al
/// terminar: el localizador lee <c>CurrentUICulture</c> en cada resolución.
/// </summary>
public class GuiaPracticanteServiceTests : IDisposable
{
    private readonly System.Globalization.CultureInfo _culturaOriginal =
        System.Globalization.CultureInfo.CurrentUICulture;

    public GuiaPracticanteServiceTests() =>
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("es");

    public void Dispose() => System.Globalization.CultureInfo.CurrentUICulture = _culturaOriginal;

    // ── Dobles ────────────────────────────────────────────────────────────

    /// <summary>Proveedor falso: registra las peticiones y responde o falla.</summary>
    private sealed class ProveedorFalso : IProveedorIaCliente
    {
        private readonly Func<GuiaPeticionIa, CancellationToken, Task<string>> _comportamiento;

        public ProveedorFalso(Func<GuiaPeticionIa, CancellationToken, Task<string>> comportamiento) =>
            _comportamiento = comportamiento;

        public static ProveedorFalso QueResponde(string respuesta) =>
            new((_, _) => Task.FromResult(respuesta));

        public static ProveedorFalso QueFalla(Exception excepcion) =>
            new((_, _) => Task.FromException<string>(excepcion));

        public List<GuiaPeticionIa> Peticiones { get; } = new();

        public Task<string> CompletarAsync(GuiaPeticionIa peticion, CancellationToken cancelacion)
        {
            Peticiones.Add(peticion);
            return _comportamiento(peticion, cancelacion);
        }
    }

    /// <summary>Proveedor que NUNCA debe ser invocado (modo catálogo).</summary>
    private sealed class ProveedorProhibido : IProveedorIaCliente
    {
        public Task<string> CompletarAsync(GuiaPeticionIa peticion, CancellationToken cancelacion) =>
            throw new InvalidOperationException(
                "El proveedor de IA no debía ser invocado en modo catálogo.");
    }

    private static GuiaPracticanteService Crear(
        IProveedorIaCliente proveedor, GuiaOpciones? opciones = null) =>
        new(
            new TecnicasDemoConsultaService(),
            proveedor,
            opciones ?? new GuiaOpciones(),
            LocalizadorDePrueba.Crear("es"),
            NullLogger<GuiaPracticanteService>.Instance);

    private static GuiaOpciones OpcionesConfiguradas() => new()
    {
        Endpoint = "https://proveedor-ia.invalid/v1/chat/completions",
        ApiKey = "clave-solo-para-pruebas-unitarias",
        Modelo = "modelo-de-pruebas"
    };

    private static GuiaConsulta Pregunta(string texto, string? nivel = null) => new(texto, nivel);

    // ── Modo catálogo (sin API key) ───────────────────────────────────────

    [Fact]
    public async Task SinApiKey_RespondeEnModoCatalogo_SinInvocarElProveedor()
    {
        var guia = Crear(new ProveedorProhibido());

        var respuesta = await guia.ResponderAsync(Pregunta("¿Qué es una guardia?"));

        respuesta.Modo.Should().Be(GuiaModo.Catalogo);
        respuesta.Texto.Should().NotBeNullOrWhiteSpace();
        respuesta.EsError.Should().BeFalse();
        guia.ModoDescripcion.Should().Contain("catálogo",
            "la etiqueta debe ser honesta: sin clave no hay IA generativa");
    }

    [Fact]
    public async Task EncuentraTecnicaPorNombre_YExplicaConPasosYPnotaDePractica()
    {
        var guia = Crear(new ProveedorProhibido());

        var respuesta = await guia.ResponderAsync(Pregunta("Mata león"));

        respuesta.Modo.Should().Be(GuiaModo.Catalogo);
        respuesta.Texto.Should().Contain("Mata león");
        respuesta.Texto.Should().Contain("alias", "los alias registrados se muestran");
        respuesta.Texto.Should().Contain("RNC");
        respuesta.Texto.Should().Contain("Pasos", "se exponen los pasos EXISTENTES de la base");
        respuesta.Texto.Should().Contain("supervisión de tu instructor",
            "toda explicación de movimientos recomienda práctica supervisada");
        respuesta.Sugerencias.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Alias_LlevaATecnicaRegistrada()
    {
        var guia = Crear(new ProveedorProhibido());

        var respuesta = await guia.ResponderAsync(Pregunta("RNC"));

        respuesta.Texto.Should().Contain("Mata león", "el alias registrado debe resolver a su técnica");
    }

    [Fact]
    public async Task TecnicaNoRegistrada_NuncaSePresentaComoExistente()
    {
        var guia = Crear(new ProveedorProhibido());

        var respuesta = await guia.ResponderAsync(
            Pregunta("Explicame la técnica Técnica Fantasma XYZ para triángulo inverso"));

        respuesta.Texto.Should().Contain("no está registrada",
            "debe decir claramente que NO está en el sistema");
        respuesta.Texto.Should().NotContain("Pasos de ejecución",
            "nunca se inventan pasos de una técnica inexistente");
    }

    [Fact]
    public async Task ListadoPorCinturon_UsaElCatalogo()
    {
        var guia = Crear(new ProveedorProhibido());

        var respuesta = await guia.ResponderAsync(Pregunta("Mostrame técnicas de cinturón blanco"));

        respuesta.Texto.Should().Contain("cinturón Blanco");
        respuesta.Texto.Should().Contain("Escape de montada",
            "Escape de montada pertenece al cinturón Blanco en el catálogo");
    }

    [Fact]
    public async Task MejorarAprendizaje_RespondeConOrientacion_YSugerencias()
    {
        var guia = Crear(new ProveedorProhibido());

        var respuesta = await guia.ResponderAsync(Pregunta("¿Cómo puedo mejorar mi aprendizaje?"));

        respuesta.Texto.Should().Contain("Practicá");
        respuesta.Sugerencias.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Concepto_Guardia_Explica_YRelacionaTecnicasDelCatalogo()
    {
        var guia = Crear(new ProveedorProhibido());

        var respuesta = await guia.ResponderAsync(Pregunta("¿Qué es una guardia?"));

        respuesta.Texto.Should().Contain("guardia");
        respuesta.Texto.Should().Contain("Técnicas del catálogo relacionadas",
            "las relaciones salen de posiciones/nombres EXISTENTES");
    }

    // ── Validación de longitud (defensa en profundidad del servicio) ──────

    [Fact]
    public async Task PreguntaExcesivamenteLarga_SinFallo_YNoSuperaElLimite()
    {
        var proveedor = ProveedorFalso.QueResponde("ok");
        var guia = Crear(proveedor, OpcionesConfiguradas());

        var respuesta = await guia.ResponderAsync(
            Pregunta(new string('a', GuiaLimites.MaxCaracteresPregunta + 500)));

        respuesta.EsError.Should().BeFalse();
        proveedor.Peticiones.Single().Pregunta.Length
            .Should().BeLessThanOrEqualTo(GuiaLimites.MaxCaracteresPregunta,
                "el límite se aplica ANTES de salir del servidor");
    }

    // ── Ruta IA (con doble falso: jamás red real) ─────────────────────────

    [Fact]
    public async Task IaConfigurada_YFuncionando_UsaModoIa()
    {
        var proveedor = ProveedorFalso.QueResponde("Respuesta generada por la IA.");
        var guia = Crear(proveedor, OpcionesConfiguradas());

        var respuesta = await guia.ResponderAsync(Pregunta("¿Qué es una guardia?"));

        respuesta.Modo.Should().Be(GuiaModo.InteligenciaArtificial);
        respuesta.Texto.Should().Be("Respuesta generada por la IA.");
        proveedor.Peticiones.Should().HaveCount(1);
        guia.ModoDescripcion.Should().Contain("IA");
    }

    [Fact]
    public async Task Ia_EnviaElNivelDelUsuario_YNuncaDatosPrivados()
    {
        var proveedor = ProveedorFalso.QueResponde("ok");
        var guia = Crear(proveedor, OpcionesConfiguradas());

        await guia.ResponderAsync(Pregunta("¿Qué es la montada?", nivel: "Azul"));

        var peticion = proveedor.Peticiones.Single();
        peticion.Sistema.Should().Contain("cinturón): Azul",
            "el nivel del usuario adapta la explicación");
        peticion.Sistema.Should().NotContain("@", "sin correos electrónicos");
        peticion.Sistema.Should().NotContain("Abcdef1!", "sin contraseñas");
        peticion.Sistema.Should().Contain("Biblioteca registrada",
            "el catálogo de SQL Server es la única fuente");
    }

    [Fact]
    public async Task Ia_SinNivel_NoSeInventaUno()
    {
        var proveedor = ProveedorFalso.QueResponde("ok");
        var guia = Crear(proveedor, OpcionesConfiguradas());

        await guia.ResponderAsync(Pregunta("¿Qué es la montada?"));

        proveedor.Peticiones.Single().Sistema.Should().Contain("desconocido");
    }

    [Fact]
    public async Task Ia_TiempoExcedido_CaefALCatalogo_ConNotaVisible()
    {
        var proveedor = ProveedorFalso.QueFalla(new TaskCanceledException("timeout simulado"));
        var guia = Crear(proveedor, OpcionesConfiguradas());

        var respuesta = await guia.ResponderAsync(Pregunta("¿Qué es una guardia?"));

        respuesta.Modo.Should().Be(GuiaModo.Catalogo,
            "ante un timeout se responde con el catálogo");
        respuesta.Nota.Should().Contain("tardó demasiado",
            "el usuario debe VER que la respuesta no vino de la IA");
        respuesta.Texto.Should().Contain("guardia");
    }

    [Fact]
    public async Task Ia_FallaDeRed_CaefALCatalogo_ConNotaVisible()
    {
        var proveedor = ProveedorFalso.QueFalla(
            new HttpRequestException("fallo de red simulado"));
        var guia = Crear(proveedor, OpcionesConfiguradas());

        var respuesta = await guia.ResponderAsync(Pregunta("Mostrame técnicas de cinturón blanco"));

        respuesta.Modo.Should().Be(GuiaModo.Catalogo);
        respuesta.Nota.Should().Contain("No se pudo contactar al servicio de IA");
        respuesta.Texto.Should().Contain("cinturón Blanco");
    }

    [Fact]
    public async Task Ia_ConRespuestaVacia_CaefALCatalogo()
    {
        var proveedor = ProveedorFalso.QueResponde("   ");
        var guia = Crear(proveedor, OpcionesConfiguradas());

        var respuesta = await guia.ResponderAsync(Pregunta("¿Qué es una guardia?"));

        respuesta.Modo.Should().Be(GuiaModo.Catalogo);
        respuesta.Texto.Should().NotBeNullOrWhiteSpace();
    }

    // ── Configuración parcial ─────────────────────────────────────────────

    [Fact]
    public async Task ConfiguracionIncompleta_NoIntentaIa()
    {
        // Solo clave, sin endpoint/modelo válido → no configurado.
        var opciones = new GuiaOpciones { ApiKey = "clave-suelta" };
        var guia = Crear(new ProveedorProhibido(), opciones);

        var respuesta = await guia.ResponderAsync(Pregunta("¿Qué es una guardia?"));

        respuesta.Modo.Should().Be(GuiaModo.Catalogo);
        guia.ModoDescripcion.Should().Contain("catálogo");
    }
}
