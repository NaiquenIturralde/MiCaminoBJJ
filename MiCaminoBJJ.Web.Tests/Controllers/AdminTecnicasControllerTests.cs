using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Web.Controllers;
using MiCaminoBJJ.Web.Services.Admin;
using MiCaminoBJJ.Web.ViewModels;
using MiCaminoBJJ.Web.ViewModels.Admin;

namespace MiCaminoBJJ.Web.Tests.Controllers;

/// <summary>
/// Etapa 3 — comportamiento del AdminTecnicasController con servicios FALSOS
/// (sin base de datos): validaciones de ModelState, rechazos de negocio,
/// 404 por Id inexistente, mensajes TempData y manejo de fallos de infraestructura.
/// La persistencia real se prueba por separado (Persistence/AdminTecnicasIntegrationTests).
/// </summary>
public class AdminTecnicasControllerTests
{
    private readonly FakeQueryService _consultas = new();
    private readonly FakeCommandService _comandos = new();
    private readonly FakeMediaQueryService _mediaConsultas = new();
    private readonly AdminTecnicasController _sut;

    public AdminTecnicasControllerTests()
    {
        _sut = new AdminTecnicasController(_consultas, _comandos, _mediaConsultas, NullLogger<AdminTecnicasController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        _sut.TempData = new TempDataDictionary(new DefaultHttpContext(), new VacioTempDataProvider());
    }

    // ── Validación de ViewModels (DataAnnotations reales) ─────────────────

    private static AdminTecnicaFormViewModel ModeloValido() => new()
    {
        Nombre = "Técnica de prueba",
        Descripcion = "Descripción de prueba.",
        Objetivo = "Objetivo de prueba.",
        Pasos = "Paso uno\nPaso dos",
        CinturonId = 1,
        CategoriaId = 1,
        PosicionId = 1
    };

    [Fact]
    public async Task Create_POST_ConModeloInvalido_NoEjecutaComando_YReRenderiza()
    {
        var model = new AdminTecnicaFormViewModel(); // todo vacío → inválido
        ValidarEnModelState(model, _sut);

        var result = await _sut.Create(model);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.Model.Should().BeSameAs(model);
        _sut.ModelState.IsValid.Should().BeFalse();
        _sut.ModelState[nameof(AdminTecnicaFormViewModel.Nombre)]!.Errors.Should().NotBeEmpty();
        _comandos.Llamadas.Should().Be(0, "un modelo inválido no debe llegar al servicio");
        _consultas.OpcionesCargadas.Should().BeTrue("el formulario debe re-renderizar con los desplegables");
    }

    [Fact]
    public async Task Create_POST_Valido_EjecutaComando_Redirige_YMensajeDeExito()
    {
        var model = ModeloValido();
        ValidarEnModelState(model, _sut);
        _comandos.Resultado = ResultadoOperacion.Ok(77);

        var result = await _sut.Create(model);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AdminTecnicasController.Index));
        _comandos.Llamadas.Should().Be(1);
        _comandos.UltimoComando!.Nombre.Should().Be("Técnica de prueba");
        _comandos.UltimoComando!.CinturonId.Should().Be(1);
        _comandos.UltimoComando!.PasosJson.Should().Be("[\"Paso uno\",\"Paso dos\"]",
            "los pasos deben serializarse a JSON en el mismo formato del dominio");
        _sut.TempData["Exito"].Should().NotBeNull();
    }

    [Fact]
    public async Task Create_POST_ConRechazoDeNegocio_AgregaErrores_YNoRedirige()
    {
        var model = ModeloValido();
        ValidarEnModelState(model, _sut);
        _comandos.Resultado = ResultadoOperacion.Fallo("Ya existe una técnica con ese nombre (la comparación ignora tildes, mayúsculas y espacios).");

        var result = await _sut.Create(model);

        result.Should().BeOfType<ViewResult>();
        _sut.ModelState[string.Empty]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("Ya existe");
        _sut.TempData["Exito"].Should().BeNull();
    }

    // ── Overposting (Etapa 4) ─────────────────────────────────────────────

    [Fact]
    public async Task Create_POST_IgnoraElIdDelFormulario()
    {
        var model = ModeloValido();
        model.Id = 77999; // valor que un cliente malicioso podría enviar
        ValidarEnModelState(model, _sut);
        _comandos.Resultado = ResultadoOperacion.Ok(501);

        var result = await _sut.Create(model);

        result.Should().BeOfType<RedirectToActionResult>();
        _comandos.UltimoComando!.Id.Should().Be(0,
            "la creación no admite Id del cliente: la identidad la asigna SQL Server");
    }

    [Fact]
    public void Formulario_Admin_NoExpone_PropiedadesNoAutorizadas()
    {
        // Overposting: el formulario no puede contener ni vincular propiedades
        // de estado o de persistencia (IsActive, NameNormalized, fechas, etc.).
        var prohibidas = new[]
        {
            nameof(Core.Entities.Technique.IsActive),
            nameof(Core.Entities.Technique.NameNormalized),
            nameof(Core.Entities.Technique.CreatedAt),
            nameof(Core.Entities.Technique.UpdatedAt)
        };

        var expuestas = typeof(AdminTecnicaFormViewModel)
            .GetProperties()
            .Where(p => p.CanWrite && prohibidas.Contains(p.Name))
            .Select(p => p.Name)
            .ToList();

        expuestas.Should().BeEmpty(
            "el ViewModel de formulario no debe exponer {0}", string.Join(", ", prohibidas));
    }

    // ── Edición ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_GET_ConIdInexistente_Responde404()
    {
        var result = await _sut.Edit(999999);
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Edit_GET_ConIdValido_CargaModelo_YDeserializaPasos()
    {
        _consultas.Tecnicas.Add(new Technique
        {
            Id = 5,
            Name = "Técnica existente",
            Description = "Desc.",
            Objective = "Obj.",
            Steps = "[\"Paso A\",\"Paso B\"]",
            BeltId = 1,
            CategoryId = 1,
            PositionId = 1,
            IsActive = true
        });

        var result = await _sut.Edit(5);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<AdminTecnicaFormViewModel>().Subject;
        model.Id.Should().Be(5);
        model.Nombre.Should().Be("Técnica existente");
        model.Pasos.Should().Be($"Paso A{Environment.NewLine}Paso B");
        model.CinturonId.Should().Be(1);
        _consultas.OpcionesCargadas.Should().BeTrue();
    }

    [Fact]
    public async Task Edit_POST_ConIdInexistente_Responde404()
    {
        var model = ModeloValido();
        ValidarEnModelState(model, _sut);
        _comandos.Resultado = ResultadoOperacion.NoEncontrada();

        var result = await _sut.Edit(999999, model);

        result.Should().BeOfType<NotFoundResult>();
        _comandos.UltimoId.Should().Be(999999, "el Id de la ruta es la fuente de verdad, no el del formulario");
    }

    // ── Desactivar / Reactivar ────────────────────────────────────────────

    [Fact]
    public async Task Deactivate_POST_EjecutaComando_YRedirigeConMensaje()
    {
        _comandos.Resultado = ResultadoOperacion.Ok();

        var result = await _sut.Deactivate(3);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AdminTecnicasController.Index));
        _comandos.Llamadas.Should().Be(1);
        _comandos.UltimoId.Should().Be(3);
        _comandos.UltimoDesactivar.Should().BeTrue();
        _sut.TempData["Exito"].Should().NotBeNull();
    }

    [Fact]
    public async Task Reactivate_POST_EjecutaComando_YRedirigeConMensaje()
    {
        _comandos.Resultado = ResultadoOperacion.Ok();

        var result = await _sut.Reactivate(4);

        result.Should().BeOfType<RedirectToActionResult>();
        _comandos.Llamadas.Should().Be(1);
        _comandos.UltimoId.Should().Be(4);
        _comandos.UltimoReactivar.Should().BeTrue();
        _sut.TempData["Exito"].Should().NotBeNull();
    }

    [Fact]
    public async Task Deactivate_POST_ConIdInexistente_Responde404()
    {
        _comandos.Resultado = ResultadoOperacion.NoEncontrada();

        var result = await _sut.Deactivate(999999);

        result.Should().BeOfType<NotFoundResult>();
        _sut.TempData["Exito"].Should().BeNull();
    }

    // ── Fallos de infraestructura (la transacción la revierte el servicio) ─

    [Fact]
    public async Task Create_POST_SiElServicioFalla_RedirigeConError_NuncaExito()
    {
        var model = ModeloValido();
        ValidarEnModelState(model, _sut);
        _comandos.Excepcion = new InvalidOperationException("fallo simulado de SQL Server");

        var result = await _sut.Create(model);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AdminTecnicasController.Index));
        _sut.TempData["Error"].Should().NotBeNull();
        _sut.TempData["Exito"].Should().BeNull();
    }

    // ── Listado ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Index_MapeaEntidades_AFilaViewModels()
    {
        _consultas.Tecnicas.Add(new Technique
        {
            Id = 1,
            Name = "Técnica visible",
            Belt = new Belt { Id = 1, Name = "Blanco", DisplayOrder = 1 },
            Category = new TechniqueCategory { Id = 1, Name = "Escape" },
            Position = new Position { Id = 1, Name = "Montada" },
            IsActive = false,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        var result = await _sut.Index("q", estado: false);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<AdminTecnicasIndexViewModel>().Subject;
        model.Tecnicas.Should().ContainSingle();
        var fila = model.Tecnicas[0];
        fila.Nombre.Should().Be("Técnica visible");
        fila.Activa.Should().BeFalse();
        fila.Cinturon.Should().Be("Blanco");
        _consultas.UltimoBuscar.Should().Be("q");
        _consultas.UltimoEstado.Should().BeFalse();
    }

    // ── Filtro por cinturón (tercer criterio independiente) ───────────────

    [Fact]
    public async Task Index_PasaLosTresFiltros_AlServicioDeConsultas()
    {
        _consultas.Tecnicas.Add(new Technique
        {
            Id = 1,
            Name = "Kimura violeta",
            BeltId = 3,
            IsActive = false,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        var result = await _sut.Index("kimura", estado: false, cinturon: 3);

        var model = result.Should().BeOfType<ViewResult>().Subject
            .Model.Should().BeOfType<AdminTecnicasIndexViewModel>().Subject;

        _consultas.UltimoBuscar.Should().Be("kimura");
        _consultas.UltimoEstado.Should().BeFalse();
        _consultas.UltimoCinturon.Should().Be(3, "el cinturón viaja como tercer criterio");
        _consultas.CatalogoCargado.Should().BeTrue("las opciones salen del catálogo existente");

        model.Cinturon.Should().Be(3);
        model.Cinturones.Should().HaveCount(5, "Blanco, Azul, Violeta, Marrón y Negro");
        model.Tecnicas.Should().ContainSingle().Which.Nombre.Should().Be("Kimura violeta");
        model.TieneFiltros.Should().BeTrue();
        model.NombreCinturon.Should().Be("Violeta");
    }

    [Fact]
    public async Task Index_SoloConCinturon_DevuelveSoloEseCinturon_ConLaBusquedaVacia()
    {
        _consultas.Tecnicas.Add(new Technique
        {
            Id = 1,
            Name = "Una violeta",
            BeltId = 3,
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        _consultas.Tecnicas.Add(new Technique
        {
            Id = 2,
            Name = "Una azul",
            BeltId = 2,
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        var result = await _sut.Index(cinturon: 3);

        var model = result.Should().BeOfType<ViewResult>().Subject
            .Model.Should().BeOfType<AdminTecnicasIndexViewModel>().Subject;

        _consultas.UltimoBuscar.Should().BeNull("el buscador de nombre está vacío");
        _consultas.UltimoCinturon.Should().Be(3);
        model.Tecnicas.Should().ContainSingle()
            .Which.Nombre.Should().Be("Una violeta", "solo el filtro de cinturón alcanza");
        model.TieneFiltros.Should().BeTrue();
    }

    [Fact]
    public async Task Index_CombinaBuscarCinturonYEstado_SoloDevuelveLoQueCumpleLosTres()
    {
        _consultas.Tecnicas.Add(new Technique
        {
            Id = 1,
            Name = "Kimura violeta activa",
            BeltId = 3,
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        _consultas.Tecnicas.Add(new Technique
        {
            Id = 2,
            Name = "Kimura violeta desactivada",
            BeltId = 3,
            IsActive = false,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        _consultas.Tecnicas.Add(new Technique
        {
            Id = 3,
            Name = "Kimura azul activa",
            BeltId = 2,
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        var result = await _sut.Index("kimura", estado: true, cinturon: 3);

        var model = result.Should().BeOfType<ViewResult>().Subject
            .Model.Should().BeOfType<AdminTecnicasIndexViewModel>().Subject;

        _consultas.UltimoBuscar.Should().Be("kimura");
        _consultas.UltimoEstado.Should().BeTrue();
        _consultas.UltimoCinturon.Should().Be(3);
        model.Tecnicas.Should().ContainSingle()
            .Which.Nombre.Should().Be("Kimura violeta activa");
    }

    [Fact]
    public async Task Index_SinFiltros_NoMarcaTieneFiltros_YTraeElCatalogo()
    {
        _consultas.Tecnicas.Add(new Technique
        {
            Id = 1,
            Name = "Sin filtros",
            BeltId = 1,
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        var result = await _sut.Index();

        var model = result.Should().BeOfType<ViewResult>().Subject
            .Model.Should().BeOfType<AdminTecnicasIndexViewModel>().Subject;

        _consultas.UltimoCinturon.Should().BeNull();
        model.Cinturon.Should().BeNull();
        model.TieneFiltros.Should().BeFalse();
        model.QueryString.Should().BeEmpty();
        model.Cinturones.Should().HaveCount(5);
    }

    // ── Conservación de los filtros entre pantallas ───────────────────────

    [Fact]
    public async Task Edit_GET_CargaLosFiltrosDelListado_EnElFormulario()
    {
        _consultas.Tecnicas.Add(new Technique
        {
            Id = 5,
            Name = "Existente",
            Description = "d",
            Objective = "o",
            BeltId = 1,
            CategoryId = 1,
            PositionId = 1,
            IsActive = true
        });

        var result = await _sut.Edit(5, buscar: "kimura", estado: false, cinturon: 3);

        var model = result.Should().BeOfType<ViewResult>().Subject
            .Model.Should().BeOfType<AdminTecnicaFormViewModel>().Subject;

        model.FiltroBuscar.Should().Be("kimura");
        model.FiltroEstado.Should().BeFalse();
        model.FiltroCinturon.Should().Be(3);
        model.RutaListado.Should().Be("/AdminTecnicas?buscar=kimura&estado=false&cinturon=3");
    }

    [Fact]
    public async Task Edit_POST_ConservaLosTresFiltros_AlVolverAlListado()
    {
        var model = ModeloValido();
        model.FiltroBuscar = "kimura";
        model.FiltroEstado = false;
        model.FiltroCinturon = 3;
        ValidarEnModelState(model, _sut);
        _comandos.Resultado = ResultadoOperacion.Ok(9);

        var result = await _sut.Edit(9, model);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AdminTecnicasController.Index));
        redirect.RouteValues!["buscar"].Should().Be("kimura");
        redirect.RouteValues["estado"].Should().Be(false);
        redirect.RouteValues["cinturon"].Should().Be(3);
    }

    [Fact]
    public async Task Create_POST_ConservaLosFiltros_AlVolverAlListado()
    {
        var model = ModeloValido();
        model.FiltroCinturon = 3;
        ValidarEnModelState(model, _sut);
        _comandos.Resultado = ResultadoOperacion.Ok(42);

        var result = await _sut.Create(model);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AdminTecnicasController.Index));
        redirect.RouteValues!["cinturon"].Should().Be(3);
        redirect.RouteValues.Should().NotContainKey("buscar", "sin filtro no se agrega el parámetro");
        redirect.RouteValues.Should().NotContainKey("estado");
    }

    [Fact]
    public async Task Deactivate_POST_ConservaLosFiltros_AlVolverAlListado()
    {
        _comandos.Resultado = ResultadoOperacion.Ok();

        var result = await _sut.Deactivate(3, buscar: "kimura", estado: true, cinturon: 3);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AdminTecnicasController.Index));
        redirect.RouteValues!["buscar"].Should().Be("kimura");
        redirect.RouteValues["estado"].Should().Be(true);
        redirect.RouteValues["cinturon"].Should().Be(3);
    }

    [Fact]
    public async Task Reactivate_POST_SinFiltros_VuelveAlListadoLimpio()
    {
        _comandos.Resultado = ResultadoOperacion.Ok();

        var result = await _sut.Reactivate(4);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AdminTecnicasController.Index));
        redirect.RouteValues.Should().BeEmpty("sin filtros la URL queda limpia");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Replica la validación de DataAnnotations que hace MVC en el pipeline.</summary>
    private static void ValidarEnModelState(AdminTecnicaFormViewModel model, Controller sut)
    {
        var resultados = new List<ValidationResult>();
        var contexto = new ValidationContext(model);
        if (Validator.TryValidateObject(model, contexto, resultados, validateAllProperties: true))
            return;

        foreach (var error in resultados)
        {
            foreach (var member in error.MemberNames)
                sut.ModelState.AddModelError(member, error.ErrorMessage!);
            if (!error.MemberNames.Any())
                sut.ModelState.AddModelError(string.Empty, error.ErrorMessage!);
        }
    }

    private sealed class FakeQueryService : IAdminTecnicasQueryService
    {
        public List<Technique> Tecnicas { get; } = new();
        public bool OpcionesCargadas { get; private set; }
        public bool CatalogoCargado { get; private set; }
        public string? UltimoBuscar { get; private set; }
        public bool? UltimoEstado { get; private set; }
        public int? UltimoCinturon { get; private set; }

        public Task<IReadOnlyList<Technique>> ListarAsync(string? buscar, bool? activa, int? cinturon)
        {
            UltimoBuscar = buscar;
            UltimoEstado = activa;
            UltimoCinturon = cinturon;

            IEnumerable<Technique> query = Tecnicas;
            if (activa.HasValue)
                query = query.Where(t => t.IsActive == activa.Value);
            if (cinturon.HasValue)
                query = query.Where(t => t.BeltId == cinturon.Value);
            return Task.FromResult<IReadOnlyList<Technique>>(query.ToList());
        }

        public Task<Technique?> ObtenerAsync(int id) =>
            Task.FromResult(Tecnicas.FirstOrDefault(t => t.Id == id));

        /// <summary>Catálogo falso con los 5 cinturones del dominio.</summary>
        public Task<IReadOnlyList<Belt>> ObtenerCinturonesAsync()
        {
            CatalogoCargado = true;
            return Task.FromResult<IReadOnlyList<Belt>>(new List<Belt>
            {
                new() { Id = 1, Name = "Blanco", DisplayOrder = 1 },
                new() { Id = 2, Name = "Azul", DisplayOrder = 2 },
                new() { Id = 3, Name = "Violeta", DisplayOrder = 3 },
                new() { Id = 4, Name = "Marrón", DisplayOrder = 4 },
                new() { Id = 5, Name = "Negro", DisplayOrder = 5 }
            });
        }

        public Task<AdminOpcionesViewModel> ObtenerOpcionesAsync()
        {
            OpcionesCargadas = true;
            return Task.FromResult(new AdminOpcionesViewModel(
                new[] { new FiltroOpcionViewModel { Id = 1, Nombre = "Blanco" } },
                new[] { new FiltroOpcionViewModel { Id = 1, Nombre = "Escape" } },
                new[] { new FiltroOpcionViewModel { Id = 1, Nombre = "Montada" } }));
        }
    }

    private sealed class FakeCommandService : IAdminTecnicasCommandService
    {
        public ResultadoOperacion Resultado { get; set; } = ResultadoOperacion.Ok();
        public Exception? Excepcion { get; set; }
        public int Llamadas { get; private set; }
        public TecnicaComando? UltimoComando { get; private set; }
        public int? UltimoId { get; private set; }
        public bool UltimoDesactivar { get; private set; }
        public bool UltimoReactivar { get; private set; }

        private ResultadoOperacion Registrar(TecnicaComando? comando, int? id)
        {
            Llamadas++;
            UltimoComando = comando;
            UltimoId = id;
            if (Excepcion is not null)
                throw Excepcion;
            return Resultado;
        }

        public Task<ResultadoOperacion> CrearAsync(TecnicaComando comando, string actor) =>
            Task.FromResult(Registrar(comando, null));

        public Task<ResultadoOperacion> EditarAsync(int id, TecnicaComando comando, string actor) =>
            Task.FromResult(Registrar(comando, id));

        public Task<ResultadoOperacion> DesactivarAsync(int id, string actor)
        {
            UltimoDesactivar = true;
            return Task.FromResult(Registrar(null, id));
        }

        public Task<ResultadoOperacion> ReactivarAsync(int id, string actor)
        {
            UltimoReactivar = true;
            return Task.FromResult(Registrar(null, id));
        }
    }

    private sealed class VacioTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }

    // ── Sección multimedia (Edit) ─────────────────────────────────────────

    private sealed class FakeMediaQueryService : IAdminTecnicasMediaQueryService
    {
        public List<AdminMediaRecursoViewModel> Recursos { get; } = new();
        public int UltimoTecnicaId { get; private set; }
        public Exception? Excepcion { get; set; }

        public Task<IReadOnlyList<AdminMediaRecursoViewModel>> ObtenerMultimediaAsync(int tecnicaId)
        {
            UltimoTecnicaId = tecnicaId;
            if (Excepcion is not null)
                throw Excepcion;
            return Task.FromResult<IReadOnlyList<AdminMediaRecursoViewModel>>(Recursos.ToList());
        }
    }

    [Fact]
    public async Task Edit_GET_CargaLaSeccionMultimedia_ConFiltrosYEstadoVacio()
    {
        _consultas.Tecnicas.Add(new Technique
        {
            Id = 5,
            Name = "Existente",
            Description = "d",
            Objective = "o",
            BeltId = 1,
            CategoryId = 1,
            PositionId = 1,
            IsActive = true
        });
        _mediaConsultas.Recursos.Add(new AdminMediaRecursoViewModel
        {
            MediaId = 10,
            Titulo = "Video demo",
            Url = "https://youtu.be/eDUjKxV6wb0",
            EsPortada = true
        });

        var result = await _sut.Edit(5, buscar: "kimura", estado: false, cinturon: 3);

        var model = result.Should().BeOfType<ViewResult>().Subject
            .Model.Should().BeOfType<AdminTecnicaFormViewModel>().Subject;

        model.Multimedia.Should().NotBeNull("Edit siempre carga la sección multimedia");
        model.Multimedia!.TecnicaId.Should().Be(5);
        model.Multimedia.Recursos.Should().ContainSingle()
            .Which.Titulo.Should().Be("Video demo");
        model.Multimedia.Vacio.Should().BeFalse();
        model.Multimedia.FiltroBuscar.Should().Be("kimura");
        model.Multimedia.FiltroEstado.Should().BeFalse();
        model.Multimedia.FiltroCinturon.Should().Be(3);
        _mediaConsultas.UltimoTecnicaId.Should().Be(5);
    }

    [Fact]
    public async Task Edit_GET_ConFalloDeConsultaMultimedia_SigueRenderizando_EstadoVacio()
    {
        _consultas.Tecnicas.Add(new Technique
        {
            Id = 5,
            Name = "Existente",
            Description = "d",
            Objective = "o",
            BeltId = 1,
            CategoryId = 1,
            PositionId = 1,
            IsActive = true
        });
        _mediaConsultas.Excepcion = new InvalidOperationException("SQL caído");

        var result = await _sut.Edit(5);

        var model = result.Should().BeOfType<ViewResult>().Subject
            .Model.Should().BeOfType<AdminTecnicaFormViewModel>().Subject;

        model.Multimedia.Should().NotBeNull("un fallo de multimedia no tira abajo la edición");
        model.Multimedia!.Recursos.Should().BeEmpty();
        model.Multimedia.Vacio.Should().BeTrue();
    }

    [Fact]
    public async Task Edit_GET_RepoblaElIntentoDeAltaFallidoDesdeTempData()
    {
        _consultas.Tecnicas.Add(new Technique
        {
            Id = 5,
            Name = "Existente",
            Description = "d",
            Objective = "o",
            BeltId = 1,
            CategoryId = 1,
            PositionId = 1,
            IsActive = true
        });
        _sut.TempData["MediaUrlIntentada"] = "https://youtube.com/shorts/abc";
        _sut.TempData["MediaTituloIntentado"] = "Titular intentado";

        var result = await _sut.Edit(5);

        var model = result.Should().BeOfType<ViewResult>().Subject
            .Model.Should().BeOfType<AdminTecnicaFormViewModel>().Subject;

        model.Multimedia!.Alta.Url.Should().Be("https://youtube.com/shorts/abc");
        model.Multimedia.Alta.Titulo.Should().Be("Titular intentado");
    }

    [Fact]
    public async Task Create_GET_NoCargaSeccionMultimedia()
    {
        var result = await _sut.Create();

        var model = result.Should().BeOfType<ViewResult>().Subject
            .Model.Should().BeOfType<AdminTecnicaFormViewModel>().Subject;

        model.Multimedia.Should().BeNull("Crear no tiene técnica: la sección no existe");
        _mediaConsultas.UltimoTecnicaId.Should().Be(0, "no se consulta multimedia en Create");
    }
}
