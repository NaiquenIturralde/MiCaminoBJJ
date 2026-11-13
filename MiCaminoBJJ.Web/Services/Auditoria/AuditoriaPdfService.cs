using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MiCaminoBJJ.Web.Services.Auditoria;

/// <summary>
/// Generador del PDF de auditoría con QuestPDF (licencia Community: uso libre
/// para proyectos como el TFI; la clave de empresa no es necesaria).
///
/// Contenido del documento:
/// - Título "Mi Camino BJJ — Reporte de auditoría".
/// - Fecha de generación y usuario que lo generó.
/// - Tabla con fecha, usuario, operación y entidad afectada.
/// - Mensaje explícito cuando no hay registros.
///
/// Privacidad: el documento incluye SOLO actor, operación, entidad, id y
/// resultado. No se incluyen contraseñas, hashes, tokens ni el detalle JSON
/// crudo de los registros.
/// </summary>
public sealed class AuditoriaPdfService : IAuditoriaPdfService
{
    private const string ColorEncabezado = "#D7B83E";
    private const string ColorEncabezadoTexto = "#3A2E08";
    private const string ColorTexto = "#1F1F1F";
    private const string ColorFilaAlterna = "#F7F4E6";

    static AuditoriaPdfService()
    {
        // Licencia Community de QuestPDF (sin clave de empresa) para el TFI.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> GenerarAsync(
        IReadOnlyList<AuditoriaFila> filas,
        string usuarioGenerador,
        DateTimeOffset generadoUtc,
        CancellationToken cancellationToken = default)
    {
        var registros = filas ?? Array.Empty<AuditoriaFila>();
        var usuario = string.IsNullOrWhiteSpace(usuarioGenerador) ? "Administrador" : usuarioGenerador.Trim();
        var fechaGeneracion = generadoUtc.ToLocalTime();

        byte[] pdf = Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.A4.Landscape());
                pagina.Margin(1.2f, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(8.5f).FontColor(ColorTexto));

                // ── Encabezado ──────────────────────────────────────────────
                pagina.Header().Element(c => c.Column(columna =>
                {
                    columna.Item()
                        .Text("Mi Camino BJJ — Reporte de auditoría")
                        .SemiBold()
                        .FontSize(16)
                        .FontColor(ColorEncabezadoTexto);
                    columna.Item()
                        .Text($"Fecha de generación: {fechaGeneracion.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)}")
                        .FontSize(9.5f);
                    columna.Item()
                        .Text($"Usuario que generó el reporte: {usuario}")
                        .FontSize(9.5f);
                    columna.Item()
                        .Text($"Registros incluidos: {registros.Count}")
                        .FontSize(9.5f);
                    columna.Item()
                        .PaddingBottom(4)
                        .LineHorizontal(1)
                        .LineColor(ColorEncabezado);
                }));

                // ── Cuerpo ──────────────────────────────────────────────────
                pagina.Content().Element(c =>
                {
                    if (registros.Count == 0)
                    {
                        c.AlignCenter()
                            .PaddingTop(40)
                            .Text("No hay registros de auditoría para los criterios seleccionados.")
                            .SemiBold()
                            .FontSize(12)
                            .FontColor("#6B6B6B");

                        c.AlignCenter()
                            .PaddingTop(6)
                            .Text("Generado por Mi Camino BJJ. Sin registros no hay actividad administrativa que reportar.")
                            .FontSize(9)
                            .FontColor("#8A8A8A");

                        return;
                    }

                    c.Table(tabla =>
                    {
                        tabla.ColumnsDefinition(columnas =>
                        {
                            columnas.ConstantColumn(115); // Fecha
                            columnas.RelativeColumn(3);    // Usuario
                            columnas.RelativeColumn(2);    // Operación
                            columnas.RelativeColumn(3);    // Entidad
                            columnas.ConstantColumn(55);   // Identificador
                            columnas.RelativeColumn(2);    // Resultado
                        });

                        tabla.Header(cabecera =>
                        {
                            cabecera.Cell().Background(ColorEncabezado).Padding(4)
                                .Element(c => c.Text("FECHA").SemiBold().FontSize(8));
                            cabecera.Cell().Background(ColorEncabezado).Padding(4)
                                .Element(c => c.Text("USUARIO").SemiBold().FontSize(8));
                            cabecera.Cell().Background(ColorEncabezado).Padding(4)
                                .Element(c => c.Text("OPERACIÓN").SemiBold().FontSize(8));
                            cabecera.Cell().Background(ColorEncabezado).Padding(4)
                                .Element(c => c.Text("ENTIDAD AFECTADA").SemiBold().FontSize(8));
                            cabecera.Cell().Background(ColorEncabezado).Padding(4)
                                .Element(c => c.Text("ID").SemiBold().FontSize(8));
                            cabecera.Cell().Background(ColorEncabezado).Padding(4)
                                .Element(c => c.Text("RESULTADO").SemiBold().FontSize(8));
                        });

                        var indice = 0;
                        foreach (var fila in registros)
                        {
                            var fondo = indice % 2 == 0
                                ? Color.FromHex("#FFFFFF")
                                : Color.FromHex(ColorFilaAlterna);
                            indice++;

                            var fecha = fila.FechaUtc.ToLocalTime()
                                .ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

                            tabla.Cell().Padding(3).Background(fondo)
                                .Element(c => c.Text(fecha).FontSize(8));
                            tabla.Cell().Padding(3).Background(fondo)
                                .Element(c => c.Text(fila.Usuario).FontSize(8));
                            tabla.Cell().Padding(3).Background(fondo)
                                .Element(c => c.Text(fila.Operacion).FontSize(8));
                            tabla.Cell().Padding(3).Background(fondo)
                                .Element(c => c.Text($"{fila.Entidad} (#{fila.EntidadId})").FontSize(8));
                            tabla.Cell().Padding(3).Background(fondo)
                                .Element(c => c.Text(fila.EntidadId.ToString(CultureInfo.InvariantCulture)).FontSize(8));
                            tabla.Cell().Padding(3).Background(fondo)
                                .Element(c => c.Text(fila.Resultado).FontSize(8));
                        }
                    });
                });

                // ── Pie ──────────────────────────────────────────────────────
                // Nota: Text(Action<TextDescriptor>) no devuelve valor, así que
                // el tamaño se toma del estilo de texto por defecto de la página.
                pagina.Footer().Element(c => c.AlignCenter().Text(texto =>
                {
                    texto.Span("Mi Camino BJJ — Reporte de auditoría · Página ");
                    texto.CurrentPageNumber();
                    texto.Span(" de ");
                    texto.TotalPages();
                }));
            });
        }).GeneratePdf();

        return Task.FromResult(pdf);
    }
}
