namespace MiCaminoBJJ.Web.Services.Guia;

/// <summary>
/// Agente conversacional MVP "Guía del Practicante": acompaña el aprendizaje de
/// BJJ usando como FUENTE PRINCIPAL las técnicas registradas en SQL Server.
///
/// Contrato (Etapa 5):
/// - Nunca afirma que una técnica está registrada si no lo está en la biblioteca.
/// - Nunca inventa progreso, entrenamientos ni datos personales del usuario.
/// - No expone contraseñas, hashes, emails, tokens, datos de otros usuarios
///   ni información de auditoría (el único dato de perfil que recibe es el
///   nivel de cinturón).
/// - Modo honesto: <see cref="ModoDescripcion"/> dice si respondió con IA o
///   con el catálogo; las fallas/timeout del proveedor caen a catálogo.
/// </summary>
public interface IGuiaPracticanteService
{
    /// <summary>Descripción del modo disponible HOY (para la interfaz).</summary>
    string ModoDescripcion { get; }

    /// <summary>Responde a la pregunta del practicante.</summary>
    Task<GuiaRespuesta> ResponderAsync(GuiaConsulta consulta, CancellationToken cancelacion = default);
}
