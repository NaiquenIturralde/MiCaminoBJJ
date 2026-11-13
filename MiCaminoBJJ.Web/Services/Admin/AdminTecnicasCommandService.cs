using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Web.Identity;
using MiCaminoBJJ.Web.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MiCaminoBJJ.Web.Services.Admin;

/// <summary>
/// Comandos administrativos de técnicas sobre SQL Server (Etapa 3).
///
/// Reglas de dominio (idénticas a TechniqueService de Infrastructure):
/// - <c>NameNormalized = TextNormalizer.Normalize(Name)</c> en alta y edición.
/// - Alta con <c>IsActive = true</c> y <c>CreatedAt = UTC</c>; edición con
///   <c>UpdatedAt = UTC</c>.
/// - Auditoría con <c>RecordStrictAsync</c>: si la auditoría falla, la
///   excepción revierte la transacción conjunta (operación + auditoría
///   se confirman o revierten juntas).
/// - Validación de duplicados por nombre normalizado (requisito de la web;
///   el servicio compartido no la incluye) y existencia de las FK.
/// - Sin eliminación física: solo bandera IsActive (SoftDelete / Restore).
///
/// UserId de auditoría = null: la web todavía no tiene usuarios persistidos
/// (no se crean usuarios ficticios) y el actor autenticado de Development se
/// registra en <c>Details["Actor"]</c> desde la cookie.
/// </summary>
public sealed class AdminTecnicasCommandService : IAdminTecnicasCommandService
{
    private const string EntityName = nameof(Technique);
    private const int MaxNameLength = 200;
    private const int MaxDescriptionLength = 2000;
    private const int MaxObjectiveLength = 1000;
    private const int MaxPasosLength = 4000;
    private const int MaxTipsLength = 4000;

    private readonly WebTfDbContext _db;
    private readonly IAuditService _audit;

    public AdminTecnicasCommandService(WebTfDbContext db, IAuditService audit)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public async Task<ResultadoOperacion> CrearAsync(TecnicaComando comando, string actor)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var errores = await ValidarComandoAsync(comando, idExistente: null);
        if (errores.Count > 0)
            return ResultadoOperacion.Fallo(errores);

        var tecnica = new Technique
        {
            Name = comando.Nombre.Trim(),
            NameNormalized = TextNormalizer.Normalize(comando.Nombre),
            Description = comando.Descripcion.Trim(),
            Objective = comando.Objetivo.Trim(),
            Steps = comando.PasosJson,
            Tips = comando.ConsejosJson,
            BeltId = comando.CinturonId,
            CategoryId = comando.CategoriaId,
            PositionId = comando.PosicionId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await using var transaccion = await _db.Database.BeginTransactionAsync();

        _db.Techniques.Add(tecnica);
        await _db.SaveChangesAsync();

        await _audit.RecordStrictAsync(
            userId: await ResolverActorUserIdAsync(actor),
            AuditAction.Create,
            EntityName,
            tecnica.Id,
            changes: new Dictionary<string, object?>
            {
                ["Actor"] = actor,
                ["Name"] = tecnica.Name,
                ["BeltId"] = tecnica.BeltId,
                ["CategoryId"] = tecnica.CategoryId,
                ["PositionId"] = tecnica.PositionId,
                ["IsActive"] = tecnica.IsActive
            });

        await transaccion.CommitAsync();
        return ResultadoOperacion.Ok(tecnica.Id);
    }

    public async Task<ResultadoOperacion> EditarAsync(int id, TecnicaComando comando, string actor)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var errores = await ValidarComandoAsync(comando, idExistente: id);
        if (errores.Count > 0)
            return ResultadoOperacion.Fallo(errores);

        await using var transaccion = await _db.Database.BeginTransactionAsync();

        var tecnica = await _db.Techniques.FindAsync(id);
        if (tecnica is null)
            return ResultadoOperacion.NoEncontrada();

        var previousName = tecnica.Name;
        var previousBeltId = tecnica.BeltId;
        var previousCategoryId = tecnica.CategoryId;
        var previousPositionId = tecnica.PositionId;

        tecnica.Name = comando.Nombre.Trim();
        tecnica.NameNormalized = TextNormalizer.Normalize(comando.Nombre);
        tecnica.Description = comando.Descripcion.Trim();
        tecnica.Objective = comando.Objetivo.Trim();
        tecnica.Steps = comando.PasosJson;
        tecnica.Tips = comando.ConsejosJson;
        tecnica.BeltId = comando.CinturonId;
        tecnica.CategoryId = comando.CategoriaId;
        tecnica.PositionId = comando.PosicionId;
        tecnica.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _audit.RecordStrictAsync(
            userId: await ResolverActorUserIdAsync(actor),
            AuditAction.Update,
            EntityName,
            tecnica.Id,
            changes: new Dictionary<string, object?>
            {
                ["Actor"] = actor,
                ["OldName"] = previousName,
                ["Name"] = tecnica.Name,
                ["OldBeltId"] = previousBeltId,
                ["BeltId"] = tecnica.BeltId,
                ["OldCategoryId"] = previousCategoryId,
                ["CategoryId"] = tecnica.CategoryId,
                ["OldPositionId"] = previousPositionId,
                ["PositionId"] = tecnica.PositionId
            });

        await transaccion.CommitAsync();
        return ResultadoOperacion.Ok(tecnica.Id);
    }

    public Task<ResultadoOperacion> DesactivarAsync(int id, string actor) =>
        CambiarEstadoAsync(id, activa: false, actor);

    public Task<ResultadoOperacion> ReactivarAsync(int id, string actor) =>
        CambiarEstadoAsync(id, activa: true, actor);

    private async Task<ResultadoOperacion> CambiarEstadoAsync(int id, bool activa, string actor)
    {
        // Ocultar = eliminación lógica; restaurar = reversión (misma
        // correspondencia de acciones que TechniqueService.SetActiveAsync).
        var action = activa ? AuditAction.Restore : AuditAction.SoftDelete;

        await using var transaccion = await _db.Database.BeginTransactionAsync();

        var tecnica = await _db.Techniques.FindAsync(id);
        if (tecnica is null)
            return ResultadoOperacion.NoEncontrada();

        tecnica.IsActive = activa;
        tecnica.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _audit.RecordStrictAsync(
            userId: await ResolverActorUserIdAsync(actor),
            action,
            EntityName,
            tecnica.Id,
            changes: new Dictionary<string, object?>
            {
                ["Actor"] = actor,
                ["IsActive"] = activa
            });

        await transaccion.CommitAsync();
        return ResultadoOperacion.Ok(tecnica.Id);
    }

    /// <summary>
    /// Resuelve el identificador del usuario de DOMINIO (<c>Users.Id</c>) del
    /// actor autenticado, para que la auditoría identifique al administrador
    /// con su id y no solo con el nombre.
    ///
    /// - La búsqueda es por <c>UserName</c> o <c>Email</c> de la cuenta de
    ///   Identity; el vínculo con el perfil de dominio es
    ///   <c>ApplicationUser.DomainUserId</c>.
    /// - Si el actor no es una cuenta de Identity (por ejemplo, la cookie
    ///   administrativa de Development) NO hay usuario de dominio: se devuelve
    ///   null y el registro conserva el nombre del actor en
    ///   <c>Details["Actor"]</c>. El registro nunca falla por esto.
    /// - No se lee ningún campo sensible (ni hash, ni token, ni correo del
    ///   actor más allá del vínculo de identidad).
    /// </summary>
    private async Task<int?> ResolverActorUserIdAsync(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
            return null;

        var nombre = actor.Trim();

        return await _db.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(u => u.UserName == nombre || u.Email == nombre)
            .Select(u => u.DomainUserId)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Validaciones de negocio (más allá de DataAnnotations): obligatoriedad y
    /// longitudes del dominio, FK existentes y duplicados por nombre normalizado.
    /// </summary>
    private async Task<List<string>> ValidarComandoAsync(TecnicaComando comando, int? idExistente)
    {
        var errores = new List<string>();

        if (string.IsNullOrWhiteSpace(comando.Nombre))
            errores.Add("El nombre es obligatorio.");
        else if (comando.Nombre.Trim().Length > MaxNameLength)
            errores.Add($"El nombre no puede superar los {MaxNameLength} caracteres.");

        if (string.IsNullOrWhiteSpace(comando.Descripcion))
            errores.Add("La descripción es obligatoria.");
        else if (comando.Descripcion.Length > MaxDescriptionLength)
            errores.Add($"La descripción no puede superar los {MaxDescriptionLength} caracteres.");

        if (string.IsNullOrWhiteSpace(comando.Objetivo))
            errores.Add("El objetivo es obligatorio.");
        else if (comando.Objetivo.Length > MaxObjectiveLength)
            errores.Add($"El objetivo no puede superar los {MaxObjectiveLength} caracteres.");

        if (comando.PasosJson?.Length > MaxPasosLength)
            errores.Add($"Los pasos no pueden superar los {MaxPasosLength} caracteres serializados.");

        if (comando.ConsejosJson?.Length > MaxTipsLength)
            errores.Add($"Los consejos no pueden superar los {MaxTipsLength} caracteres serializados.");

        if (errores.Count > 0)
            return errores;

        // Integridad referencial de las relaciones del dominio.
        if (!await _db.Belts.AnyAsync(b => b.Id == comando.CinturonId))
            errores.Add("El cinturón seleccionado no existe.");

        if (!await _db.TechniqueCategories.AnyAsync(c => c.Id == comando.CategoriaId))
            errores.Add("La categoría seleccionada no existe.");

        if (!await _db.Positions.AnyAsync(p => p.Id == comando.PosicionId))
            errores.Add("La posición seleccionada no existe.");

        // Duplicados con la normalización existente (misma regla de TextNormalizer).
        var normalizado = TextNormalizer.Normalize(comando.Nombre);
        if (normalizado.Length > 0)
        {
            var duplicada = await _db.Techniques.AnyAsync(t =>
                t.NameNormalized == normalizado &&
                t.Id != (idExistente ?? -1));

            if (duplicada)
                errores.Add("Ya existe una técnica con ese nombre (la comparación ignora tildes, mayúsculas y espacios).");
        }

        return errores;
    }
}
