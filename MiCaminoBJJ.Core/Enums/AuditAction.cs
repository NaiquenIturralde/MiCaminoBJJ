namespace MiCaminoBJJ.Core.Enums;

/// <summary>
/// Acción de negocio registrada en AuditLog.
/// Persistida como int (HasConversion&lt;int&gt;), agregar valores al final
/// para no reasignar significados a registros existentes.
/// </summary>
public enum AuditAction
{
    /// <summary>Alta de un registro.</summary>
    Create = 0,

    /// <summary>Modificación de un registro existente.</summary>
    Update = 1,

    /// <summary>Eliminación lógica (bandera), sin DELETE físico.</summary>
    SoftDelete = 2,

    /// <summary>Restauración tras una eliminación lógica.</summary>
    Restore = 3,

    /// <summary>Eliminación física definitiva (purga).</summary>
    Purge = 4,

    /// <summary>Inicio de sesión exitoso.</summary>
    LoginSuccess = 5,

    /// <summary>Intento de inicio de sesión fallido.</summary>
    LoginFailed = 6,

    /// <summary>Operación denegada por permisos insuficientes.</summary>
    PermissionDenied = 7,

    /// <summary>Cambio de estado de usuario (activo, suspendido, baja solicitada, etc.).</summary>
    StatusChange = 8
}
