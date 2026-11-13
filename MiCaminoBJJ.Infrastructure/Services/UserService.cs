using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<UserService> _logger;

    public UserService(AppDbContext context, IPasswordHasher passwordHasher, ILogger<UserService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<User?> GetByIdAsync(int userId)
    {
        return await _context.Users
            .Include(u => u.Settings)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail.Length == 0)
            return null;

        // SQLite compara TEXT de forma sensible a mayúsculas/minúsculas:
        // se compara en minúsculas para que el login reconozca el correo
        // registrado con cualquier combinación de mayúsculas.
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail.Length == 0)
            return false;

        return await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
    }

    public async Task<User> CreateAsync(string name, string email, string password, string beltRank, string? academy)
    {
        var hash = _passwordHasher.HashPassword(password, out var salt);

        var user = new User
        {
            Name = name.Trim(),
            // Se guarda normalizado (recortado y en minúsculas) para que el
            // índice único de Users.Email también impida duplicados por casing.
            Email = NormalizeEmail(email),
            PasswordHash = hash,
            PasswordSalt = salt,
            BeltRank = beltRank,
            Academy = academy,
            Role = UserRole.Practicante
        };

        _context.Users.Add(user);

        // Create default settings
        var settings = new UserSettings
        {
            User = user,
            Theme = "dark",
            Language = "es",
            NotificationsEnabled = true
        };
        _context.UserSettings.Add(settings);

        await _context.SaveChangesAsync();

        _logger.LogInformation("User created: {Email} (Id={UserId})", email, user.Id);
        return user;
    }

    public async Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ValidateCredentialsAsync(string email, string password)
    {
        var user = await GetByEmailAsync(email);
        if (user == null)
            return false;

        return _passwordHasher.VerifyPassword(password, user.PasswordHash, user.PasswordSalt);
    }

    private static string NormalizeEmail(string? email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();
}
