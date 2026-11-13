using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Web.Identity;
using MiCaminoBJJ.Web.Persistence;

namespace MiCaminoBJJ.Web.Security;

/// <summary>
/// Restablecimiento de la contraseña del administrador EXISTENTE (Etapa 4D-C).
///
/// Uso (solo Development, base autorizada MiCaminoBJJ_Web_TFI_2026):
///   dotnet run --project MiCaminoBJJ.Web -- reset-admin-password
///
/// Por qué existe: la contraseña de la cuenta web vive SOLO en
/// <c>AspNetUsers.PasswordHash</c> (PBKDF2 de Identity). Si se pierde, no hay
/// recuperación posible (el proyecto no tiene SMTP ni flujo de "olvidé mi
/// contraseña"), y las contraseñas de MAUI (SQLite) y de <c>AdminDev:Password</c>
/// (módulo local de Development) NO sirven para el login público.
///
/// Diferencias DELIBERADAS con <see cref="AdminBootstrap"/> (init-admin):
/// - init-admin CREA una cuenta nueva y RECHAZA emails existentes (exit 6);
///   por eso no sirve para recuperar una cuenta perdida.
/// - Este comando NO crea nada: LOCALIZA la cuenta existente, verifica que
///   esté vinculada al perfil de dominio esperado y que conserve el rol
///   Administrador, y solo entonces cambia la contraseña.
/// - No crea cuentas, no crea roles, no asigna roles, no borra registros y no
///   toca a ningún otro usuario.
///
/// Diseño de seguridad (idéntico al patrón ya auditado de init-admin):
/// - Ejecución EXPLÍCITA por argumento: nunca corre al arrancar la aplicación.
/// - La contraseña NUNCA viaja por argumentos, archivos ni variables de
///   entorno: se lee de forma interactiva (tecla a tecla con '*', sin eco) o
///   —solo para pruebas— por proveedores inyectados que no usan la consola.
/// - Requiere entorno Development y el nombre EXACTO de la base autorizada
///   (se verifica antes de tocar nada; la lectura es local, sin consultar).
/// - Verifica que las migraciones estén aplicadas; NUNCA migra.
/// - Candado exclusivo sp_getapplock por sesión (dos ejecuciones simultáneas
///   no pueden pasar la ventana de "verificar y escribir").
/// - Respaldo PREVIO verificable (BACKUP DATABASE + RESTORE VERIFYONLY) antes de
///   abrir la transacción de escritura.
/// - Todo o nada: cambio de hash + desbloqueo + auditoría en UNA transacción
///   sobre el WebTfDbContext compartido (los SaveChanges de UserManager y del
///   servicio de auditoría participan en ella).
/// - El cambio de contraseña ROTA el SecurityStamp (Identity lo hace): las
///   cookies emitidas antes dejan de ser válidas.
/// - Auditoría sin contraseñas, hashes ni tokens (solo actor, operación,
///   email, vínculo de dominio y verificación de rol).
///
/// Códigos de salida:
///  0 ok · 1 uso · 2 entorno · 3 base · 4 migraciones · 5 entrada inválida ·
///  6 cuenta no encontrada · 7 concurrencia · 8 vínculo de dominio incorrecto ·
///  9 error interno · 10 rol Administrador ausente · 11 respaldo fallido.
/// </summary>
public static class ResetAdminPassword
{
    public const string Comando = "reset-admin-password";

    public const int ExitOk = 0;
    public const int ExitUso = 1;
    public const int ExitEntorno = 2;
    public const int ExitBase = 3;
    public const int ExitMigraciones = 4;
    public const int ExitEntrada = 5;
    public const int ExitCuentaNoEncontrada = 6;
    public const int ExitConcurrencia = 7;
    public const int ExitVinculo = 8;
    public const int ExitInterno = 9;
    public const int ExitRol = 10;
    public const int ExitRespaldo = 11;

    private const string RolAdministrador = "Administrador";
    private const string ActorAuditoria = "reset-admin-password-local";
    private const string OperacionAuditoria = "ResetAdminPassword";
    private const string RecursosLock = "MiCaminoBJJ_Web_TFI_ResetAdminPassword";
    private const string FraseConfirmacion = "CONFIRMAR";
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Insumos de entrada: en la CLI son lectores de consola; en pruebas se
    /// inyectan en memoria. La contraseña jamás llega por línea de comandos.
    /// </summary>
    public sealed record Insumos(
        Func<string?> LeerEmail,
        Func<string?> LeerPassword,
        Func<string?> LeerPasswordRepetida,
        Func<string?> LeerConfirmacion);

    /// <summary>
    /// Parámetros del procedimiento. Los valores por defecto son los de
    /// PRODUCCIÓN LOCAL (perfil 339 sobre la base exclusiva de la web); las
    /// pruebas los reemplazan para operar sobre bases aisladas.
    /// </summary>
    public sealed record Opciones(
        int DomainUserIdEsperado = 339,
        string? BaseEsperada = null,
        string? CarpetaRespaldo = null,
        bool EjecutarRespaldo = true);

    public static bool EsComando(string[] args) =>
        args is { Length: > 0 } && args[0].Equals(Comando, StringComparison.OrdinalIgnoreCase);

    public static async Task<int> EjecutarAsync(
        IServiceProvider servicios,
        IHostEnvironment entorno,
        string[] args,
        Insumos? insumos = null,
        Opciones? opciones = null)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(entorno);
        ArgumentNullException.ThrowIfNull(args);

        var opc = opciones ?? new Opciones();

        // ── Uso: solo el comando y ayuda; jamás secretos por argumentos ──
        if (args.Any(a => a is "--help" or "-h"))
        {
            MostrarAyuda();
            return ExitUso;
        }

        if (args.Length != 1)
        {
            Console.Error.WriteLine("RECHAZADO: la herramienta solo acepta 'reset-admin-password'.");
            Console.Error.WriteLine("Las credenciales NUNCA se pasan por argumentos.");
            return ExitUso;
        }

        var db = servicios.GetRequiredService<WebTfDbContext>();
        var userManager = servicios.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = servicios.GetRequiredService<RoleManager<IdentityRole>>();
        var auditoria = servicios.GetRequiredService<IAuditService>();
        var validadoresPassword = servicios.GetServices<IPasswordValidator<ApplicationUser>>().ToList();

        try
        {
            // ── 1. Entorno: exclusivamente Development ──
            if (!entorno.IsDevelopment())
            {
                Console.Error.WriteLine(
                    "RECHAZADO: reset-admin-password solo está permitido en entornos Development.");
                return ExitEntorno;
            }

            // ── 2. Base autorizada: servidor + base EXACTOS (lectura local) ──
            var esperada = opc.BaseEsperada ?? WebSqlServerOptions.DatabaseName;
            var conexionEf = db.Database.GetDbConnection();
            var cadenaConexionEfectiva = conexionEf.ConnectionString ?? string.Empty;
            var builderConexion = new SqlConnectionStringBuilder(cadenaConexionEfectiva);
            var servidorEfectivo = builderConexion.DataSource;
            var nombreBaseEfectivo = builderConexion.InitialCatalog;

            if (string.IsNullOrWhiteSpace(servidorEfectivo)
                || !string.Equals(servidorEfectivo, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine(
                    $"RECHAZADO: el servidor de conexión '{servidorEfectivo}' no está autorizado. "
                    + "Solo se permite (localdb)\\MSSQLLocalDB.");
                return ExitBase;
            }

            if (string.IsNullOrWhiteSpace(nombreBaseEfectivo)
                || !string.Equals(nombreBaseEfectivo, esperada, StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine(
                    $"RECHAZADO: la conexión apunta a '{nombreBaseEfectivo}' y no a la base autorizada '{esperada}'.");
                return ExitBase;
            }

            // ── 3. Migraciones aplicadas (solo verifica; NUNCA migra) ──
            var pendientes = (await db.Database.GetPendingMigrationsAsync()).ToList();
            if (pendientes.Count > 0)
            {
                Console.Error.WriteLine(
                    "RECHAZADO: hay migraciones pendientes: " + string.Join(", ", pendientes));
                Console.Error.WriteLine(
                    "Este comando NO migra. Aplicá las migraciones manualmente y volvé a intentar.");
                return ExitMigraciones;
            }

            // ── 4. Candado exclusivo por base (sesión) ──
            var cadena = db.Database.GetConnectionString()
                ?? throw new InvalidOperationException("La conexión no tiene cadena de conexión.");
            // Pooling=false: el candado es de SESIÓN y solo se libera cuando la
            // conexión REAL se cierra; con pooling quedaría retenido en el pool.
            var cadenaCandado = cadena.Contains("Pooling", StringComparison.OrdinalIgnoreCase)
                ? cadena
                : cadena + ";Pooling=false";
            await using var candado = new SqlConnection(cadenaCandado);
            await candado.OpenAsync();
            if (await AdquirirCandadoAsync(candado) < 0)
            {
                Console.Error.WriteLine(
                    "RECHAZADO: hay otro mantenimiento administrativo en curso (candado exclusivo).");
                return ExitConcurrencia;
            }

            var insumo = insumos ?? new Insumos(
                LeerEmailConsola,
                ConsolaSegura.LeerPassword,
                ConsolaSegura.LeerPassword,
                LeerConfirmacionConsola);

            // ── 5. Email de la cuenta (identifica el destino) ──
            Console.Write("Email del administrador: ");
            var email = insumo.LeerEmail()?.Trim();
            if (string.IsNullOrWhiteSpace(email) || !EsEmailValido(email))
            {
                Console.Error.WriteLine("RECHAZADO: el email no tiene un formato válido.");
                return ExitEntrada;
            }

            // ── 6. Cuenta EXISTENTE en Identity (por email normalizado) ──
            var identity = await userManager.FindByEmailAsync(email)
                ?? await userManager.FindByNameAsync(email);
            if (identity is null)
            {
                Console.Error.WriteLine(
                    "RECHAZADO: no existe una cuenta de Identity con ese email. "
                    + "Este comando NO crea cuentas (para crear la primera usá init-admin).");
                return ExitCuentaNoEncontrada;
            }

            // ── 7. Vínculo con el perfil de dominio esperado (1:1) ──
            if (identity.DomainUserId != opc.DomainUserIdEsperado)
            {
                Console.Error.WriteLine(
                    $"RECHAZADO: la cuenta está vinculada a DomainUserId={identity.DomainUserId?.ToString() ?? "(null)"} "
                    + $"y se esperaba {opc.DomainUserIdEsperado}. No se modifica nada.");
                return ExitVinculo;
            }

            var perfil = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == opc.DomainUserIdEsperado);
            if (perfil is null
                || !string.Equals(perfil.Email?.Trim(), email, StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine(
                    $"RECHAZADO: el perfil de dominio {opc.DomainUserIdEsperado} no existe "
                    + "o su email no coincide con la cuenta de Identity. No se modifica nada.");
                return ExitVinculo;
            }

            // ── 8. Rol Administrador VIGENTE (se verifica, jamás se asigna) ──
            if (!await roleManager.RoleExistsAsync(RolAdministrador)
                || !await userManager.IsInRoleAsync(identity, RolAdministrador))
            {
                Console.Error.WriteLine(
                    "RECHAZADO: la cuenta no tiene el rol Administrador asignado. "
                    + "Este comando NO modifica roles: la recuperación del rol requiere "
                    + "una operación autorizada aparte (AddToRoleAsync).");
                return ExitRol;
            }

            // ── 9. Contraseña nueva, dos veces, sin eco ──
            Console.Write("Contraseña nueva (8+, mayúscula, minúscula, número y especial): ");
            var password = insumo.LeerPassword();
            if (string.IsNullOrWhiteSpace(password))
            {
                Console.Error.WriteLine("RECHAZADO: la contraseña no puede estar vacía.");
                return ExitEntrada;
            }

            Console.Write("Repetí la contraseña nueva: ");
            var repetida = insumo.LeerPasswordRepetida();
            if (!string.Equals(password, repetida, StringComparison.Ordinal))
            {
                Console.Error.WriteLine("RECHAZADO: las contraseñas no coinciden.");
                return ExitEntrada;
            }

            // ── 10. Política de contraseñas CONFIGURADA (mismos validadores de Identity) ──
            var erroresPolitica = new List<string>();
            foreach (var validador in validadoresPassword)
            {
                var resultadoValidacion = await validador.ValidateAsync(userManager, identity, password);
                if (resultadoValidacion is null || resultadoValidacion.Succeeded)
                    continue;

                erroresPolitica.AddRange(resultadoValidacion.Errors.Select(e => e.Description));
            }

            if (erroresPolitica.Count > 0)
            {
                Console.Error.WriteLine("RECHAZADO: la contraseña no cumple la política configurada.");
                foreach (var error in erroresPolitica)
                    Console.Error.WriteLine("  · " + error);
                return ExitEntrada;
            }

            // ── 11. Confirmación explícita (la contraseña NO se valida por eco) ──
            Console.Write($"Escribí '{FraseConfirmacion}' para restablecer la contraseña de {email}: ");
            var confirmacion = insumo.LeerConfirmacion()?.Trim();
            if (!string.Equals(confirmacion, FraseConfirmacion, StringComparison.Ordinal))
            {
                Console.Error.WriteLine("CANCELADO: no se confirmó la operación. No se modificó nada.");
                return ExitEntrada;
            }

            // ── 12. Respaldo PREVIO verificable (antes de cualquier escritura) ──
            if (opc.EjecutarRespaldo)
            {
                var carpeta = opc.CarpetaRespaldo ?? CarpetaRespaldoPredeterminada();
                if (!await CrearRespaldoVerificadoAsync(cadenaCandado, nombreBaseEfectivo!, carpeta))
                    return ExitRespaldo;
            }
            else
            {
                Console.WriteLine("AVISO: respaldo omitido (solo permitido en pruebas automatizadas).");
            }

            // ── 13. Transacción única: contraseña + desbloqueo + auditoría ──
            await using var transaccion = await db.Database.BeginTransactionAsync();

            // 13.1 Token oficial de Identity (DataProtector) + ResetPassword.
            //      ResetPasswordAsync valida de nuevo la política y rota el
            //      SecurityStamp (invalida las cookies ya emitidas).
            var token = await userManager.GeneratePasswordResetTokenAsync(identity);
            var reset = await userManager.ResetPasswordAsync(identity, token, password);
            if (!reset.Succeeded)
            {
                await transaccion.RollbackAsync();
                Console.Error.WriteLine("RECHAZADO: " + Describir(reset.Errors));
                return ExitEntrada;
            }

            // 13.2 Desbloqueo defensivo: una cuenta bloqueada no podría usar la
            //      contraseña nueva. Solo se normaliza el estado de lockout de
            //      ESTA cuenta; no se tocan roles, vínculos ni otros usuarios.
            var desbloqueoAplicado = identity.AccessFailedCount > 0
                || (identity.LockoutEnd is not null && identity.LockoutEnd > DateTimeOffset.UtcNow);
            if (desbloqueoAplicado)
            {
                var sinIntentos = await userManager.ResetAccessFailedCountAsync(identity);
                if (!sinIntentos.Succeeded)
                    throw new InvalidOperationException(
                        "No se pudo reiniciar el contador de intentos: " + Describir(sinIntentos.Errors));

                var sinBloqueo = await userManager.SetLockoutEndDateAsync(identity, null);
                if (!sinBloqueo.Succeeded)
                    throw new InvalidOperationException(
                        "No se pudo quitar el bloqueo: " + Describir(sinBloqueo.Errors));
            }

            // 13.3 Auditoría estricta: si falla, se revierte TODO (incluida la
            //      contraseña). Sin contraseñas, hashes ni tokens.
            await auditoria.RecordStrictAsync(
                userId: null,
                action: AuditAction.Update,
                entityType: nameof(User),
                entityId: opc.DomainUserIdEsperado,
                result: AuditResult.Success,
                changes: new Dictionary<string, object?>
                {
                    ["Actor"] = ActorAuditoria,
                    ["Operacion"] = OperacionAuditoria,
                    ["Email"] = email,
                    ["DomainUserId"] = opc.DomainUserIdEsperado,
                    ["RolAdministradorVerificado"] = true,
                    ["DesbloqueoAplicado"] = desbloqueoAplicado
                });

            await transaccion.CommitAsync();

            Console.WriteLine(
                $"OK: contraseña restablecida para el perfil de dominio {opc.DomainUserIdEsperado} "
                + $"(rol {RolAdministrador} verificado).");
            Console.WriteLine("La contraseña y el token NO quedaron registrados en ningún log ni en la auditoría.");
            Console.WriteLine("Las sesiones abiertas con la contraseña anterior dejaron de ser válidas.");
            return ExitOk;
        }
        catch (Exception ex)
        {
            // Sin Commit → la transacción se revierte al disposal: nada queda a medias.
            Console.Error.WriteLine("ERROR: el restablecimiento se revirtió. " + ex.Message);
            return ExitInterno;
        }
    }

    // ── Respaldo verificable ──────────────────────────────────────────────

    /// <summary>
    /// Carpeta de respaldos FUERA del repositorio (misma que usan los respaldos
    /// manuales del proyecto). Se crea si no existe: LocalDB no crea carpetas.
    /// </summary>
    public static string CarpetaRespaldoPredeterminada()
    {
        var documentos = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentos, "MiCaminoBJJ_respaldos");
    }

    private static async Task<bool> CrearRespaldoVerificadoAsync(
        string cadenaConexion, string nombreBase, string carpeta)
    {
        try
        {
            Directory.CreateDirectory(carpeta);

            var sello = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var archivo = Path.Combine(
                carpeta, $"{nombreBase}_preResetPassword_{sello}.bak");

            Console.WriteLine($"Respaldo previo: {archivo}");

            // BACKUP DATABASE no admite parámetros: se escapa la ruta (comillas
            // simples duplicadas) y el nombre de base se valida antes.
            var archivoEscapado = archivo.Replace("'", "''");
            var baseEscapada = nombreBase.Replace("]", "]]");

            await using var conexion = new SqlConnection(cadenaConexion);
            await conexion.OpenAsync();

            await using (var backup = conexion.CreateCommand())
            {
                backup.CommandText =
                    $"BACKUP DATABASE [{baseEscapada}] TO DISK = N'{archivoEscapado}' "
                    + "WITH COPY_ONLY, INIT, CHECKSUM, NAME = N'Respaldo previo a reset-admin-password';";
                backup.CommandTimeout = 120;
                await backup.ExecuteNonQueryAsync();
            }

            await using (var verificar = conexion.CreateCommand())
            {
                verificar.CommandText = $"RESTORE VERIFYONLY FROM DISK = N'{archivoEscapado}' WITH CHECKSUM;";
                verificar.CommandTimeout = 120;
                await verificar.ExecuteNonQueryAsync();
            }

            if (!File.Exists(archivo))
            {
                Console.Error.WriteLine("RECHAZADO: el respaldo no dejó archivo en disco.");
                return false;
            }

            var tamano = new FileInfo(archivo).Length;
            if (tamano <= 0)
            {
                Console.Error.WriteLine("RECHAZADO: el archivo de respaldo está vacío.");
                return false;
            }

            Console.WriteLine($"Respaldo verificado (RESTORE VERIFYONLY correcto, {tamano / 1024} KB).");
            return true;
        }
        catch (Exception ex)
        {
            // Fail closed: sin respaldo verificado NO se escribe nada.
            Console.Error.WriteLine(
                "RECHAZADO: no se pudo crear o verificar el respaldo previo. No se modificó nada. "
                + ex.Message);
            return false;
        }
    }

    // ── Candado exclusivo por base (sp_getapplock, sesión) ────────────────

    private static async Task<int> AdquirirCandadoAsync(SqlConnection conexion)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandText = """
            DECLARE @resultado int;
            EXEC @resultado = sp_getapplock
                @Resource = @recurso,
                @LockMode = 'Exclusive',
                @LockOwner = 'Session',
                @LockTimeout = @timeoutMs;
            SELECT @resultado;
            """;
        comando.Parameters.AddWithValue("@recurso", RecursosLock);
        comando.Parameters.AddWithValue("@timeoutMs", (int)LockTimeout.TotalMilliseconds);
        var resultado = await comando.ExecuteScalarAsync();
        return Convert.ToInt32(resultado);
    }

    // ── Validaciones y entrada ────────────────────────────────────────────

    private static bool EsEmailValido(string email) =>
        new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email);

    /// <summary>Errores de Identity sin exponer la contraseña ni el token.</summary>
    private static string Describir(IEnumerable<IdentityError> errores) =>
        string.Join("; ", errores.Select(e => e.Description));

    private static string? LeerEmailConsola()
    {
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("RECHAZADO: se requiere entrada interactiva (no hay consola).");
            return null;
        }

        return Console.ReadLine();
    }

    /// <summary>
    /// Confirmación explícita: NO es secreta (se muestra al escribir). Se exige
    /// una palabra literal para evitar restablecimientos accidentales.
    /// </summary>
    private static string? LeerConfirmacionConsola()
    {
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("RECHAZADO: se requiere entrada interactiva (no hay consola).");
            return null;
        }

        return Console.ReadLine();
    }

    private static void MostrarAyuda()
    {
        Console.WriteLine("Uso: dotnet run --project MiCaminoBJJ.Web -- reset-admin-password");
        Console.WriteLine("Restablece la contraseña del administrador EXISTENTE (Development, base exclusiva).");
        Console.WriteLine();
        Console.WriteLine("Pasos: entorno Development → base exacta → migraciones aplicadas → candado exclusivo");
        Console.WriteLine("       → email de la cuenta → vínculo DomainUserId → rol Administrador vigente");
        Console.WriteLine("       → contraseña nueva dos veces (sin eco) → confirmación → respaldo verificado");
        Console.WriteLine("       → transacción única (hash + desbloqueo + auditoría).");
        Console.WriteLine();
        Console.WriteLine("La contraseña NUNCA se pasa por argumentos ni se registra en logs/auditoría.");
    }
}
