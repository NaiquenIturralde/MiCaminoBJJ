using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Data;
using MiCaminoBJJ.Infrastructure.Data.Seed;

namespace MiCaminoBJJ.Infrastructure.Services;

public class DatabaseInitializer
{
    private const string AdminEmail = "admin@micaminobjj.com";
    private const string PlaceholderSalt = "SeedSaltPlaceholder";
    private const string DefaultAdminPassword = "CONTRASENA_ELIMINADA_EN_LIMPIEZA_DE_SEGURIDAD";

    private readonly AppDbContext _context;
    private readonly SeedDataService _seedService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        AppDbContext context,
        SeedDataService seedService,
        IPasswordHasher passwordHasher,
        ILogger<DatabaseInitializer> logger)
    {
        _context = context;
        _seedService = seedService;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing database...");

            // Ensure database is created and migrations applied
            await _context.Database.MigrateAsync();

            // Seed initial data
            await _seedService.SeedAsync();

            // Repair existing admin placeholder password if needed
            await RepairAdminPasswordAsync();

            _logger.LogInformation("Database initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database initialization failed.");
            throw;
        }
    }

    private async Task RepairAdminPasswordAsync()
    {
        try
        {
            var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == AdminEmail);
            if (adminUser == null)
            {
                _logger.LogInformation("Admin user '{Email}' not found. No repair needed.", AdminEmail);
                return;
            }

            if (adminUser.PasswordSalt != PlaceholderSalt)
            {
                _logger.LogInformation("Admin user '{Email}' already has a valid password. No repair needed.", AdminEmail);
                return;
            }

            adminUser.PasswordHash = _passwordHasher.HashPassword(DefaultAdminPassword, out var newSalt);
            adminUser.PasswordSalt = newSalt;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin user '{Email}' password repaired with default development password.", AdminEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to repair admin password.");
        }
    }
}
