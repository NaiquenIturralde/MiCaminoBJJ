namespace MiCaminoBJJ.Web.Services.Guia;

/// <summary>Modo con el que el guía produjo una respuesta (etiqueta honesta).</summary>
public enum GuiaModo
{
    /// <summary>Respuesta armada a partir del catálogo de SQL Server (sin IA generativa).</summary>
    Catalogo,

    /// <summary>Respuesta generada por el proveedor de IA externo.</summary>
    InteligenciaArtificial
}

/// <summary>
/// Consulta que recibe el guía. El NIVEL de cinturón es el ÚNICO dato del
/// perfil que viaja (nunca email, contraseña, id ni datos de otros usuarios).
/// </summary>
public sealed record GuiaConsulta(string Texto, string? NivelCinturon);

/// <summary>Respuesta del guía hacia la capa web.</summary>
public sealed record GuiaRespuesta(
    string Texto,
    GuiaModo Modo,
    IReadOnlyList<string> Sugerencias,
    string? Nota = null,
    bool EsError = false);

/// <summary>Turno de conversación enviado al proveedor de IA (solo texto del usuario).</summary>
public sealed record GuiaTurnoIa(string Rol, string Texto);

/// <summary>
/// Petición armada para el proveedor de IA: prompt de sistema (con el catálogo
/// y las reglas) + pregunta. No contiene datos privados del usuario.
/// </summary>
public sealed record GuiaPeticionIa(string Sistema, string Pregunta);
