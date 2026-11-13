using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Web.Controllers;
using MiCaminoBJJ.Web.Security;
using MiCaminoBJJ.Web.Services.Auditoria;
using MiCaminoBJJ.Web.ViewModels.Admin;
using Xunit;

namespace MiCaminoBJJ.Web.Tests.Controllers;

/// <summary>
/// Cierre TFI — pantalla de auditoría y descarga del PDF.
///
/// Verifica (sin base de datos) que:
/// - la descarga devuelve un archivo "application/pdf" con nombre de archivo,
/// - la pantalla muestra los registros que entrega la consulta,
/// - la política de auditoría EXIGE el rol "Administrador": un Practicante
///   autenticado NO pasa la autorización.
/// </summary>
public sealed class AuditoriaControllerTests
{
    private sealed class ConsultaFalsa : IAuditoriaConsultaService
    {
        public AuditoriaFiltro? UltimoFiltro { get; private set; }

        public Task<AuditoriaConsulta> ConsultarAsync(
            AuditoriaFiltro filtro,
            CancellationToken cancellationToken = default)
        {
            UltimoFiltro = filtro;

            var filas = new List<AuditoriaFila>
            {
                new(1, DateTime.UtcNow.AddMinutes(-5), "admin@micaminobjj.com",
                    "Creación", "Técnica", 42, "Correcto")
            };

            return Task.FromResult(new AuditoriaConsulta
            {
                Filas = filas,
                Total = filas.Count,
                Pagina = filtro.Pagina,
                TamanoPagina = filtro.TamanoPagina
            });
        }

        public Task<IReadOnlyList<string>> ObtenerEntidadesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(new[] { "Technique" });
    }

    private sealed class PdfFalso : IAuditoriaPdfService
    {
        public int Llamadas { get; private set; }

        public string? UsuarioGenerador { get; private set; }

        public Task<byte[]> GenerarAsync(
            IReadOnlyList<AuditoriaFila> filas,
            string usuarioGenerador,
            DateTimeOffset generadoUtc,
            CancellationToken cancellationToken = default)
        {
            Llamadas++;
            UsuarioGenerador = usuarioGenerador;

            // Cabecera PDF mínima y válida.
            return Task.FromResult<byte[]>(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF"));
        }
    }

    private static ClaimsPrincipal Usuario(string rol, string nombre = "admin@micaminobjj.com") =>
        new(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.Name, nombre),
                new Claim(ClaimTypes.Role, rol)
            },
            "esquema-de-prueba"));

    private static AuditoriaController CrearController(
        IAuditoriaConsultaService consulta,
        IAuditoriaPdfService pdf,
        ClaimsPrincipal? usuario = null)
    {
        var controller = new AuditoriaController(consulta, pdf, NullLogger<AuditoriaController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = usuario ?? Usuario("Administrador")
            }
        };

        return controller;
    }

    // ── Descarga del PDF ──────────────────────────────────────────────────

    [Fact]
    public async Task DescargarPdf_DevuelveElArchivoComoPdfAdjunto()
    {
        var pdf = new PdfFalso();
        var controller = CrearController(new ConsultaFalsa(), pdf, Usuario("Administrador"));

        var resultado = await controller.DescargarPdf();

        var archivo = resultado.Should().BeOfType<FileContentResult>().Subject;
        archivo.ContentType.Should().Be("application/pdf");
        archivo.FileDownloadName.Should().EndWith(".pdf");
        archivo.FileContents.Should().NotBeEmpty();
        pdf.Llamadas.Should().Be(1);
        pdf.UsuarioGenerador.Should().Be("admin@micaminobjj.com");
    }

    [Fact]
    public async Task DescargarPdf_AplicaLosFiltros_Y_ReporteTodosLosRegistros()
    {
        var consulta = new ConsultaFalsa();
        var controller = CrearController(consulta, new PdfFalso(), Usuario("Administrador"));

        await controller.DescargarPdf(
            accion: nameof(AuditAction.Create),
            entidad: "Technique",
            usuario: "admin",
            desde: new DateTime(2026, 1, 1),
            hasta: new DateTime(2026, 12, 31));

        consulta.UltimoFiltro.Should().NotBeNull();
        consulta.UltimoFiltro!.Accion.Should().Be(AuditAction.Create);
        consulta.UltimoFiltro.Entidad.Should().Be("Technique");
        consulta.UltimoFiltro.Usuario.Should().Be("admin");
        // Modo reporte: sin paginación (todos los registros del filtro).
        consulta.UltimoFiltro.TamanoPagina.Should().Be(0);
    }

    // ── Pantalla ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Index_MuestraLosRegistros_Reales()
    {
        var controller = CrearController(new ConsultaFalsa(), new PdfFalso(), Usuario("Administrador"));

        var resultado = await controller.Index();

        var vista = resultado.Should().BeOfType<ViewResult>().Subject;
        var model = vista.Model.Should().BeOfType<AuditoriaIndexViewModel>().Subject;

        model.HayRegistros.Should().BeTrue();
        model.Registros.Should().HaveCount(1);
        model.Registros[0].Usuario.Should().Be("admin@micaminobjj.com");
        model.Registros[0].Operacion.Should().Be("Creación");
        model.Registros[0].Entidad.Should().Be("Técnica");
        model.Registros[0].EntidadId.Should().Be(42);
        model.TotalRegistros.Should().Be(1);
    }

    // ── Autorización ──────────────────────────────────────────────────────

    [Fact]
    public void PoliticaDeAuditoria_ExigeElRolAdministrador()
    {
        var politica = AdminPolicies.BuildAdministradorAuditoria();

        politica.AuthenticationSchemes.Should().Contain("Identity.Application");
        politica.AuthenticationSchemes.Should().Contain("Cookies");

        Cumple(politica, Usuario("Administrador")).Should().BeTrue(
            "el administrador debe poder consultar la auditoría");
        Cumple(politica, Usuario("Practicante")).Should().BeFalse(
            "un Practicante NO puede consultar la auditoría");
        Cumple(politica, new ClaimsPrincipal(new ClaimsIdentity())).Should().BeFalse(
            "un visitante anónimo NO puede consultar la auditoría");
    }

    /// <summary>
    /// Evalúa los requisitos de la política contra un principal concreto
    /// (autenticado + roles), sin depender del esquema de autenticación.
    /// </summary>
    private static bool Cumple(AuthorizationPolicy politica, ClaimsPrincipal usuario)
    {
        foreach (var requisito in politica.Requirements)
        {
            if (requisito is DenyAnonymousAuthorizationRequirement)
            {
                if (usuario.Identity?.IsAuthenticated != true)
                    return false;
            }
            else if (requisito is RolesAuthorizationRequirement roles)
            {
                if (!roles.AllowedRoles.Any(rol => usuario.IsInRole(rol)))
                    return false;
            }
            else
            {
                // Requisito no contemplado en esta prueba: se considera no cumplido.
                return false;
            }
        }

        return true;
    }
}
