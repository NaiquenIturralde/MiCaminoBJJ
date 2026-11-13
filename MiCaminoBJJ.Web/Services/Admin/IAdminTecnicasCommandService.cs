namespace MiCaminoBJJ.Web.Services.Admin;

/// <summary>Entrada de un comando administrativo de técnica (NO es una entidad EF).</summary>
public sealed record TecnicaComando(
    int Id,
    string Nombre,
    string Descripcion,
    string Objetivo,
    string? PasosJson,
    string? ConsejosJson,
    int CinturonId,
    int CategoriaId,
    int PosicionId);

/// <summary>
/// Resultado uniforme de un comando administrativo:
/// - <see cref="Exito"/>: operación aplicada y auditada.
/// - <see cref="Ausente"/>: la técnica pedida no existe (el controlador responde 404).
/// - <see cref="Errores"/>: rechazo de validación de negocio (duplicados, FKs, longitudes).
/// </summary>
public sealed class ResultadoOperacion
{
    private static readonly IReadOnlyList<string> SinErrores = Array.Empty<string>();

    private ResultadoOperacion(bool exito, bool ausente, IReadOnlyList<string> errores, int? tecnicaId)
    {
        Exito = exito;
        Ausente = ausente;
        Errores = errores;
        TecnicaId = tecnicaId;
    }

    public bool Exito { get; }
    public bool Ausente { get; }
    public IReadOnlyList<string> Errores { get; }
    public int? TecnicaId { get; }

    public static ResultadoOperacion Ok(int? tecnicaId = null) =>
        new(true, false, SinErrores, tecnicaId);

    public static ResultadoOperacion Fallo(params string[] errores) =>
        new(false, false, errores, null);

    public static ResultadoOperacion Fallo(IEnumerable<string> errores) =>
        new(false, false, errores.ToList(), null);

    public static ResultadoOperacion NoEncontrada() =>
        new(false, true, SinErrores, null);
}

/// <summary>
/// Comandos de escritura del CRUD administrativo de técnicas (Etapa 3),
/// exclusivos de Web sobre <c>WebTfDbContext</c>.
///
/// No reutiliza <c>TechniqueService</c> (Infrastructure) porque su flujo
/// administrativo depende de <c>IAdminAuthorizationService</c>, que a su vez
/// exige <c>ISessionService</c> de MAUI, inexistente en la web. Aquí se
/// conservan las MISMAS reglas de dominio y la MISMA auditoría
/// (<c>IAuditService.RecordStrictAsync</c>), con autorización resuelta por la
/// cookie de Development en el controlador.
/// </summary>
public interface IAdminTecnicasCommandService
{
    Task<ResultadoOperacion> CrearAsync(TecnicaComando comando, string actor);
    Task<ResultadoOperacion> EditarAsync(int id, TecnicaComando comando, string actor);
    Task<ResultadoOperacion> DesactivarAsync(int id, string actor);
    Task<ResultadoOperacion> ReactivarAsync(int id, string actor);
}
