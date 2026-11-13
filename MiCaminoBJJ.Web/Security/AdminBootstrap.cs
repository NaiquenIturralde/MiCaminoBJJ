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
/// Inicialización local del primer administrador real (Etapa 4D-B).
///
/// Uso (solo Development, base autorizada MiCaminoBJJ_Web_TFI_2026):
///   dotnet run --project MiCaminoBJJ.Web -- init-admin
///
/// Diseño de seguridad:
/// - Ejecución EXPLÍCITA por argumento: nunca corre al arrancar la aplicación.
/// - La contraseña NUNCA viaja por argumentos ni se muestra: se lee de forma
///   interactiva (tecla a tecla con '*', sin eco) o —solo para pruebas— por
///   proveedores inyectados que no usan la consola.
/// - Requiere entorno Development y el nombre EXACTO de la base autorizada
///   (se verifica antes de tocar nada; la lectura del nombre es local, sin
///   contacto con el servidor).
/// - Verifica que las migraciones estén aplicadas; NUNCA migra.
/// - Protección real contra dos ejecuciones simultáneas: candado
///   sp_getapplock exclusivo por sesión sobre la base (más el índice único
///   de email como segunda capa).
/// - Todo o nada: Identity + rol + perfil de dominio + auditoría en UNA
///   transacción sobre el WebTfDbContext compartido (los SaveChanges de
///   UserManager/RoleManager/WebAuditService participan en ella).
/// - Auditoría sin contraseñas, tokens ni secretos (solo actor, operación y
///   email del operado).
///
/// Códigos de salida: 0 ok · 1 uso · 2 entorno · 3 base · 4 migraciones ·
/// 5 entrada inválida · 6 email duplicado · 7 concurrencia · 8 ya inicializado
/// 9 error interno.
/// </summary>
public static class AdminBootstrap
{
    public const string Comando = "init-admin";

    public const int ExitOk = 0;
    public const int ExitUso = 1;
    public const int ExitEntorno = 2;
    public const int ExitBase = 3;
    public const int ExitMigraciones = 4;
    public const int ExitEntrada = 5;
    public const int ExitDuplicado = 6;
    public const int ExitConcurrencia = 7;
    public const int ExitYaInicializado = 8;
    public const int ExitInterno = 9;

    private const string RecursoLock = "MiCaminoBJJ_Web_TFI_InitAdmin";
    private const string RolAdministrador = "Administrador";
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Insumos de entrada: en la CLI son lectores de consola; en pruebas se
    /// inyectan en memoria. La contraseña jamás llega por línea de comandos.
    /// </summary>
    public sealed record InsumosInitAdmin(
        Func<string?> LeerEmail,
        Func<string?> LeerPassword,
        Func<string?> LeerPasswordRepetida);

    public static bool EsComando(string[] args) =>
        args is { Length: > 0 } && args[0].Equals(Comando, StringComparison.OrdinalIgnoreCase);

    public static async Task<int> EjecutarAsync(
        IServiceProvider servicios,
        Microsoft.Extensions.Hosting.IHostEnvironment entorno,
        string[] args,
        InsumosInitAdmin? insumos = null,
        string? baseEsperada = null)
    {
        // ── Uso: solo "init-admin" y ayuda; jamás secretos por argumentos ──
        if (args.Any(a => a is "--help" or "-h"))
        {
            Console.WriteLine("Uso: dotnet run --project MiCaminoBJJ.Web -- init-admin");
            Console.WriteLine("Crea el primer administrador real (Development, base exclusiva).");
            return ExitUso;
        }

        if (args.Length != 1)
        {
            Console.Error.WriteLine("RECHAZADO: la herramienta solo acepta 'init-admin'.");
            Console.Error.WriteLine("Las credenciales NUNCA se pasan por argumentos.");
            return ExitUso;
        }

        var db = servicios.GetRequiredService<WebTfDbContext>();
        var userManager = servicios.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = servicios.GetRequiredService<RoleManager<IdentityRole>>();
        var auditoria = servicios.GetRequiredService<IAuditService>();

        try
        {
            // ── 1. Entorno: exclusivamente Development ──
            if (!entorno.IsDevelopment())
            {
                Console.Error.WriteLine("RECHAZADO: init-admin solo está permitido en entornos Development.");
                return ExitEntorno;
            }

            // ── 2. Base autorizada: servidor + base EXACTOS (lectura local, sin consultar) ──
            // D1: Validación estricta de conexión. Usar SqlConnectionStringBuilder
            // para interpretar los valores reales. Servidor: (localdb)\MSSQLLocalDB
            // Base: MiCaminoBJJ_Web_TFI_2026. Solo Development.
            var esperada = baseEsperada ?? WebSqlServerOptions.DatabaseName;
            var conexionEf = db.Database.GetDbConnection();
            var cadenaConexionEfectiva = conexionEf.ConnectionString ?? string.Empty;
            var builderConexion = new SqlConnectionStringBuilder(cadenaConexionEfectiva);
            var servidorEfectivo = builderConexion.DataSource;
            var nombreBaseEfectivo = builderConexion.InitialCatalog;

            if (string.IsNullOrWhiteSpace(servidorEfectivo) ||
                !string.Equals(servidorEfectivo, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine(
                    $"RECHAZADO: el servidor de conexión '{servidorEfectivo}' no está autorizado. Solo se permite (localdb)\\MSSQLLocalDB.");
                return ExitBase;
            }

            if (string.IsNullOrWhiteSpace(nombreBaseEfectivo) ||
                !string.Equals(nombreBaseEfectivo, esperada, StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine(
                    $"RECHAZADO: la conexión apunta a '{nombreBaseEfectivo}' y no a la base autorizada '{esperada}'.");
                return ExitBase;
            }

            // ── 3. Migraciones aplicadas (solo verifica; NUNCA migra) ──
            var pendientes = (await db.Database.GetPendingMigrationsAsync()).ToList();
            if (pendientes.Count > 0)
            {
                Console.Error.WriteLine("RECHAZADO: hay migraciones pendientes: " + string.Join(", ", pendientes));
                Console.Error.WriteLine("Ejecutá 'dotnet ef database update' sobre la base autorizada antes de inicializar.");
                return ExitMigraciones;
            }

            var insumo = insumos ?? new InsumosInitAdmin(LeerEmailConsola, LeerPasswordConsola, LeerPasswordConsola);

            // ── 4. Candado exclusivo: dos ejecuciones simultáneas no pueden
            //      pasar la ventana de "verificar y crear" al mismo tiempo ──
            var cadena = db.Database.GetConnectionString()
                ?? throw new InvalidOperationException("La conexión no tiene cadena de conexión.");
            // Pooling=false: el candado es de SESIÓN y solo se libera cuando la
            // conexión REAL se cierra; con pooling la sesión quedaría viva en el
            // pool reteniendo el candado.
            var cadenaCandado = cadena.Contains("Pooling", StringComparison.OrdinalIgnoreCase)
                ? cadena
                : cadena + ";Pooling=false";
            await using var candado = new SqlConnection(cadenaCandado);
            await candado.OpenAsync();
            var candadoObtenido = await AdquirirCandadoAsync(candado);
            if (candadoObtenido < 0)
            {
                Console.Error.WriteLine("RECHAZADO: hay otra inicialización en curso (candado exclusivo).");
                return ExitConcurrencia;
            }

            // ── 5. Email interactivo (validación de formato) ──
            Console.Write("Email del administrador: ");
            var email = insumo.LeerEmail()?.Trim();
            if (string.IsNullOrWhiteSpace(email) || !EsEmailValido(email))
            {
                Console.Error.WriteLine("RECHAZADO: el email no tiene un formato válido.");
                return ExitEntrada;
            }

            // ── 6. Una sola inicialización: si ya existe un administrador ──
            var administradores = await userManager.GetUsersInRoleAsync(RolAdministrador);
            if (administradores.Count > 0)
            {
                Console.Error.WriteLine("RECHAZADO: ya existe un administrador inicializado (inicialización repetida).");
                return ExitYaInicializado;
            }

            // ── 7. Email duplicado entre cuentas existentes ──
            if (await userManager.FindByEmailAsync(email) is not null)
            {
                Console.Error.WriteLine("RECHAZADO: ya existe una cuenta con ese email.");
                return ExitDuplicado;
            }

            // ── 8. Contraseña interactiva, dos veces, sin eco ──
            Console.Write("Contraseña (8+, mayúscula, minúscula, número y especial): ");
            var password = insumo.LeerPassword();
            Console.WriteLine();
            if (string.IsNullOrWhiteSpace(password))
            {
                Console.Error.WriteLine("RECHAZADO: la contraseña no puede estar vacía.");
                return ExitEntrada;
            }

            Console.Write("Repetí la contraseña: ");
            var repetida = insumo.LeerPasswordRepetida();
            Console.WriteLine();
            if (!string.Equals(password, repetida, StringComparison.Ordinal))
            {
                Console.Error.WriteLine("RECHAZADO: las contraseñas no coinciden.");
                return ExitEntrada;
            }

            // ── 9. Transacción única: rol + Identity + perfil + auditoría ──
            await using var transaccion = await db.Database.BeginTransactionAsync();

            if (!await roleManager.RoleExistsAsync(RolAdministrador))
            {
                var creacionRol = await roleManager.CreateAsync(new IdentityRole(RolAdministrador));
                if (!creacionRol.Succeeded)
                    throw new InvalidOperationException(
                        "No se pudo crear el rol Administrador: " + Describir(creacionRol.Errors));
            }

            var identity = new ApplicationUser { UserName = email, Email = email };
            var creacion = await userManager.CreateAsync(identity, password);
            if (!creacion.Succeeded)
            {
                await transaccion.RollbackAsync();
                Console.Error.WriteLine("RECHAZADO: " + Describir(creacion.Errors));
                return creacion.Errors.Any(e => e.Code.StartsWith("Duplicate", StringComparison.Ordinal))
                    ? ExitDuplicado
                    : ExitEntrada;
            }

            // Perfil de dominio vinculado (mismo patrón transaccional del registro
            // de prácticos): campos mínimos reales; PasswordHash/Salt vacíos por
            // DISEÑO (las credenciales viven solo en AspNetUsers).
            var perfil = new User
            {
                Name = email,
                Email = email,
                PasswordHash = string.Empty,
                PasswordSalt = string.Empty,
                BeltRank = "Blanco",
                Role = UserRole.Administrador,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(perfil);
            await db.SaveChangesAsync();

            identity.DomainUserId = perfil.Id;
            var vinculo = await userManager.UpdateAsync(identity);
            if (!vinculo.Succeeded)
                throw new InvalidOperationException(
                    "No se pudo vincular el perfil de dominio: " + Describir(vinculo.Errors));

            var asignacion = await userManager.AddToRoleAsync(identity, RolAdministrador);
            if (!asignacion.Succeeded)
                throw new InvalidOperationException(
                    "No se pudo asignar el rol Administrador: " + Describir(asignacion.Errors));

            // Auditoría: sin contraseñas/tokens; email del operado permitido.
            await auditoria.RecordStrictAsync(
                userId: null,
                action: AuditAction.Create,
                entityType: nameof(User),
                entityId: perfil.Id,
                result: AuditResult.Success,
                changes: new Dictionary<string, object?>
                {
                    ["Actor"] = "init-admin-local",
                    ["Operacion"] = "InitAdmin",
                    ["Email"] = email
                });

            await transaccion.CommitAsync();

            Console.WriteLine($"OK: administrador inicializado (perfil de dominio Id={perfil.Id}, rol {RolAdministrador}).");
            Console.WriteLine("La contraseña NO quedó registrada en ningún log.");
            return ExitOk;
        }
        catch (Exception ex)
        {
            // Sin Commit → la transacción se revierte al disposal: nada queda a medias.
            Console.Error.WriteLine("ERROR: la inicialización se revirtió. " + ex.Message);
            return ExitInterno;
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
        comando.Parameters.AddWithValue("@recurso", RecursoLock);
        comando.Parameters.AddWithValue("@timeoutMs", (int)LockTimeout.TotalMilliseconds);
        var resultado = await comando.ExecuteScalarAsync();
        return Convert.ToInt32(resultado);
    }

    // ── Validaciones y entrada ────────────────────────────────────────────

    private static bool EsEmailValido(string email) =>
        new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email);

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
    /// Lectura interactiva sin eco: imprime '*' por tecla. Delegada en
    /// <see cref="ConsolaSegura"/> para compartir UNA sola implementación con
    /// el comando reset-admin-password (comportamiento observable idéntico).
    /// </summary>
    private static string? LeerPasswordConsola() => ConsolaSegura.LeerPassword();
}
