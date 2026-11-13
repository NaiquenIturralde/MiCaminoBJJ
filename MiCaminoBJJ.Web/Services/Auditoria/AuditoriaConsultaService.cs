using System.Text.Json;
using MiCaminoBJJ.Core.Entities;
using Microsoft.EntityFrameworkCore;
using MiCaminoBJJ.Web.Persistence;

namespace MiCaminoBJJ.Web.Services.Auditoria;

/// <summary>
/// Implementación de <see cref="IAuditoriaConsultaService"/> sobre
/// <see cref="WebTfDbContext"/> (SQL Server de la web).
///
/// Reutiliza la tabla <c>AuditLogs</c> y la entidad <see cref="AuditLog"/> del
/// Core: no hay tablas nuevas ni migraciones.
///
/// Origen del actor (en este orden):
/// 1. <c>AuditLog.UserId</c> → usuario de dominio (Users) con su nombre y correo.
/// 2. <c>Details["Actor"]</c> → actor registrado por la operación (cookie de
///    Development o proceso de servicio).
/// 3. "Sistema" → operaciones sin actor (por ejemplo, el seed inicial).
/// </summary>
public sealed class AuditoriaConsultaService : IAuditoriaConsultaService
{
    /// <summary>Tope de registros en modo reporte (protección ante filtros vacíos).</summary>
    public const int LimiteMaximoRegistros = 5000;

    private readonly WebTfDbContext _db;

    public AuditoriaConsultaService(WebTfDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<AuditoriaConsulta> ConsultarAsync(
        AuditoriaFiltro filtro,
        CancellationToken cancellationToken = default)
    {
        var f = filtro ?? new AuditoriaFiltro();

        var query = _db.AuditLogs.AsNoTracking();

        if (f.Accion.HasValue)
        {
            var accion = f.Accion.Value;
            query = query.Where(a => a.Action == accion);
        }

        if (!string.IsNullOrWhiteSpace(f.Entidad))
        {
            var entidad = f.Entidad.Trim();
            query = query.Where(a => a.EntityType == entidad);
        }

        if (f.Desde.HasValue)
        {
            var desde = f.Desde.Value;
            query = query.Where(a => a.TimestampUtc >= desde);
        }

        if (f.Hasta.HasValue)
        {
            var hasta = f.Hasta.Value;
            query = query.Where(a => a.TimestampUtc <= hasta);
        }

        if (!string.IsNullOrWhiteSpace(f.Usuario))
        {
            var texto = f.Usuario.Trim();
            query = query.Where(a =>
                (a.Details != null && a.Details.Contains(texto))
                || _db.Users.Any(u =>
                    (int?)u.Id == a.UserId
                    && (u.Name.Contains(texto) || u.Email.Contains(texto))));
        }

        var total = await query.CountAsync(cancellationToken);

        var pagina = Math.Max(1, f.Pagina);
        var tamano = f.TamanoPagina;
        var saltar = tamano > 0 ? (pagina - 1) * tamano : 0;
        var tomar = tamano > 0 ? tamano : LimiteMaximoRegistros;

        var registros = await query
            .OrderByDescending(a => a.TimestampUtc)
            .ThenByDescending(a => a.Id)
            .Skip(saltar)
            .Take(tomar)
            .ToListAsync(cancellationToken);

        // Nombres de los actores con usuario de dominio (una sola consulta).
        var ids = registros
            .Where(a => a.UserId.HasValue)
            .Select(a => a.UserId!.Value)
            .Distinct()
            .ToList();

        var usuarios = ids.Count == 0
            ? new Dictionary<int, (string Nombre, string Correo)>()
            : await _db.Users
                .AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => (u.Name, u.Email), cancellationToken);

        var filas = registros
            .Select(a => new AuditoriaFila(
                a.Id,
                a.TimestampUtc,
                NombreActor(a, usuarios),
                AuditoriaEtiquetas.Operacion(a.Action),
                AuditoriaEtiquetas.Entidad(a.EntityType),
                a.EntityId,
                AuditoriaEtiquetas.Resultado(a.Result)))
            .ToList();

        return new AuditoriaConsulta
        {
            Filas = filas,
            Total = total,
            Pagina = pagina,
            TamanoPagina = tamano
        };
    }

    public async Task<IReadOnlyList<string>> ObtenerEntidadesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.AuditLogs
            .AsNoTracking()
            .Select(a => a.EntityType)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Nombre visible del actor: usuario de dominio cuando el registro tiene
    /// UserId; si no, el "Actor" de Details; si no, "Sistema".
    /// </summary>
    private static string NombreActor(
        AuditLog registro,
        IReadOnlyDictionary<int, (string Nombre, string Correo)> usuarios)
    {
        if (registro.UserId.HasValue
            && usuarios.TryGetValue(registro.UserId.Value, out var usuario)
            && !string.IsNullOrWhiteSpace(usuario.Nombre))
        {
            return string.IsNullOrWhiteSpace(usuario.Correo)
                ? usuario.Nombre
                : $"{usuario.Nombre} ({usuario.Correo})";
        }

        var actor = LeerActorDeDetalles(registro.Details);

        return string.IsNullOrWhiteSpace(actor) ? "Sistema" : actor;
    }

    /// <summary>
    /// Lee SOLO la clave "Actor" del JSON de Details. Cualquier otro contenido
    /// (y cualquier JSON inválido) se ignora: no se expone ni se interpreta.
    /// </summary>
    private static string? LeerActorDeDetalles(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
            return null;

        try
        {
            using var documento = JsonDocument.Parse(details);

            if (documento.RootElement.ValueKind == JsonValueKind.Object
                && documento.RootElement.TryGetProperty("Actor", out var actor)
                && actor.ValueKind == JsonValueKind.String)
            {
                var valor = actor.GetString();
                return string.IsNullOrWhiteSpace(valor) ? null : valor;
            }
        }
        catch (JsonException)
        {
            // Details no es JSON válido: se ignora (no rompe la consulta).
        }

        return null;
    }
}
