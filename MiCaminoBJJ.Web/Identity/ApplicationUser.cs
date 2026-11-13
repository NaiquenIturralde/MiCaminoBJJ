using Microsoft.AspNetCore.Identity;

namespace MiCaminoBJJ.Web.Identity;

/// <summary>
/// Usuario de IDENTIDAD de la web (ASP.NET Core Identity, Etapa 4B).
///
/// Relación con el dominio (Etapa 4B, sin registros automáticos todavía):
/// - <see cref="DomainUserId"/> → <c>Users.Id</c> (int, entidad Core de dominio)
///   mediante FK opcional con índice ÚNICO (1:1) y borrado SetNull: purgar un
///   perfil de dominio jamás borra la credencial de acceso, y viceversa.
/// - Las credenciales/tokens/roles viven SOLO en las tablas AspNet*; los datos
///   personales y de entrenamiento siguen perteneciendo a <c>User</c> de
///   dominio (sus 11 cadenas de FK no se tocan; la app MAUI es ajena a esto).
/// - Nulo hasta la Etapa 4E (el vínculo se creará al registrar/perfilar);
///   ningún registro de dominio se crea automáticamente en esta etapa.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>Id del usuario de dominio (<c>Users.Id</c>) o null si aún no hay perfil.</summary>
    public int? DomainUserId { get; set; }
}
