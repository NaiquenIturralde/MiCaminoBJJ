using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Infrastructure.Data.Configurations;
using MiCaminoBJJ.Web.Identity;

namespace MiCaminoBJJ.Web.Persistence;

/// <summary>
/// DbContext SQL Server de la web: dominio (Etapa 2A) + ASP.NET Core Identity
/// en un ÚNICO contexto (Etapa 4B) para conservar la unidad de trabajo con
/// SQL Server (identidad y datos comparten transacción y conexión).
///
/// Decisiones:
/// - NO se reutiliza <c>AppDbContext</c> (SQLite de MAUI: intacto y ajeno).
/// - NO se duplican entidades de dominio (viven en MiCaminoBJJ.Core).
/// - NO se cambia la clave primaria de <c>User</c> (int, con sus 11 FK).
/// - Identity aporta <c>ApplicationUser</c>, <c>IdentityRole</c> y las
///   tablas AspNet*; el vínculo con el dominio es
///   <c>ApplicationUser.DomainUserId → Users.Id</c> (opcional, índice único,
///   borrado SetNull) configurado en OnModelCreating sin tocar Core.
///
/// NB: <c>Users</c> de ESTA clase (propiedad `new`) sigue siendo la entidad de
/// dominio <c>User</c>; Identity accede a la suya con <c>Set&lt;ApplicationUser&gt;()</c>.
///
/// Migraciones: viven en este ensamblado (Migrations/SqlServer), separadas de
/// las de SQLite.
/// </summary>
public class WebTfDbContext : IdentityDbContext<ApplicationUser>
{
    public WebTfDbContext(DbContextOptions<WebTfDbContext> options) : base(options)
    {
    }

    public new DbSet<User> Users => Set<User>();
    /// <summary>
    /// Los 20 DbSets de dominio de abajo conservan sus nombres. `new` aquí evita
    /// el choque con <c>IdentityDbContext&lt;TUser&gt;.Users</c> (que enlazaría con
    /// <c>ApplicationUser</c>): Identity usa <c>Set&lt;ApplicationUser&gt;()</c>
    /// internamente, nunca esta propiedad, por lo que ocultarla es seguro y
    /// mantiene sin cambios el código existente que usa <c>db.Users</c> (dominio).
    /// </summary>
    public DbSet<Belt> Belts => Set<Belt>();
    public DbSet<TechniqueCategory> TechniqueCategories => Set<TechniqueCategory>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Technique> Techniques => Set<Technique>();
    public DbSet<TechniqueAlias> TechniqueAliases => Set<TechniqueAlias>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<TechniqueMedia> TechniqueMedia => Set<TechniqueMedia>();
    public DbSet<UserTechniqueResource> UserTechniqueResources => Set<UserTechniqueResource>();
    public DbSet<UserProfileMedia> UserProfileMedia => Set<UserProfileMedia>();
    public DbSet<SavedTechnique> SavedTechniques => Set<SavedTechnique>();
    public DbSet<TechniqueView> TechniqueViews => Set<TechniqueView>();
    public DbSet<TechniquePractice> TechniquePractices => Set<TechniquePractice>();
    public DbSet<UserTechniqueProgress> UserTechniqueProgress => Set<UserTechniqueProgress>();
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<UserTournament> UserTournaments => Set<UserTournament>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Reutiliza las 20 configuraciones compartidas (todas las de Infrastructure).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TechniqueConfiguration).Assembly);

        // ── Ajuste específico de SQL Server (documentado en el informe 2A) ──
        // Ruta en cascada 1: Users -> TrainingSessions (Cascade) -> TechniquePractices (Cascade)
        // Ruta en cascada 2: Users -> TechniquePractices (Cascade)
        // SQL Server rechaza crear la FK con "multiple cascade paths" (error 1785).
        // La solución sin tocar archivos compartidos es restringir el borrado desde
        // TrainingSession: borrar una sesión deja sus prácticas (UserId sigue Cascade).
        modelBuilder.Entity<TechniquePractice>()
            .HasOne(p => p.TrainingSession)
            .WithMany(p => p.Practices)
            .HasForeignKey(p => p.TrainingSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Etapa 4B: asociación Identity ↔ usuario de dominio ──
        // ApplicationUser.DomainUserId → Users.Id (int). Sin navegación en Core
        // (entidades intactas): WithMany() vacío.
        // - Índice ÚNICO: garantiza 1:1 (SQL Server permite varios NULL, que es
        //   exactamente el estado hasta crear el perfil en la Etapa 4E).
        // - Borrado SetNull: eliminar un perfil de dominio desvincula la
        //   credencial sin borrarla (y jamás al revés: AspNet* no purga Users).
        // - Único camino nuevo hacia Users y sin cascada → no introduce
        //   "multiple cascade paths" (error 1785).
        modelBuilder.Entity<ApplicationUser>(applicationUser =>
        {
            applicationUser.HasIndex(u => u.DomainUserId).IsUnique();
            applicationUser
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(u => u.DomainUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
