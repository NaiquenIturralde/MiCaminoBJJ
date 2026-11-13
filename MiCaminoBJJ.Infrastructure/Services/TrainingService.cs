using Microsoft.EntityFrameworkCore;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace MiCaminoBJJ.Infrastructure.Services;

public class TrainingService : ITrainingService
{
    private readonly AppDbContext _context;
    private readonly ITechniquePracticeService _practiceService;
    private readonly ILogger<TrainingService> _logger;

    public TrainingService(
        AppDbContext context,
        ITechniquePracticeService practiceService,
        ILogger<TrainingService> logger)
    {
        _context = context;
        _practiceService = practiceService;
        _logger = logger;
    }

    public async Task<TrainingSession> CreateAsync(int userId, TrainingWithTechniques input)
    {
        // Validaciones de negocio antes de tocar la base de datos.
        ValidateSessionData(input);
        await ValidateTechniquesAsync(input.Techniques);

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Create the training session
            var session = new TrainingSession
            {
                UserId = userId,
                TrainingType = input.TrainingType,
                Date = input.Date,
                DurationMinutes = input.DurationMinutes,
                Feeling = input.Feeling,
                Notes = input.Notes
            };

            _context.TrainingSessions.Add(session);
            await _context.SaveChangesAsync();

            // Register practices for each technique (ya validadas como existentes y activas)
            foreach (var tech in input.Techniques)
            {
                await _practiceService.RegisterPracticeAsync(
                    userId,
                    tech.TechniqueId,
                    session.Id,
                    tech.Repetitions,
                    tech.DurationMinutes,
                    tech.Confidence,
                    tech.Notes,
                    input.Date);
            }

            await transaction.CommitAsync();

            _logger.LogInformation("Training session {SessionId} created for user {UserId} with {Count} techniques",
                session.Id, userId, input.Techniques.Count);

            return session;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to create training session for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Valida los datos generales de la sesión: fecha no futura, duración 1-600,
    /// técnicas no duplicadas, repeticiones 1-1000 y minutos por técnica 0-600.
    /// </summary>
    private static void ValidateSessionData(TrainingWithTechniques input)
    {
        if (input.Date.Date > DateTime.Today)
            throw new ArgumentException("La fecha del entrenamiento no puede ser futura.", nameof(input));

        if (input.DurationMinutes < 1 || input.DurationMinutes > 600)
            throw new ArgumentOutOfRangeException(nameof(input),
                $"La duración del entrenamiento debe estar entre 1 y 600 minutos (recibido: {input.DurationMinutes}).");

        var duplicated = input.Techniques
            .GroupBy(t => t.TechniqueId)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicated != null)
            throw new ArgumentException(
                $"La técnica con identificador {duplicated.Key} está duplicada en esta sesión.", nameof(input));

        foreach (var tech in input.Techniques)
        {
            if (tech.Repetitions < 1 || tech.Repetitions > 1000)
                throw new ArgumentOutOfRangeException(nameof(input),
                    $"Las repeticiones de la técnica con identificador {tech.TechniqueId} deben estar entre 1 y 1000 (recibido: {tech.Repetitions}).");

            if (tech.DurationMinutes < 0 || tech.DurationMinutes > 600)
                throw new ArgumentOutOfRangeException(nameof(input),
                    $"Los minutos de la técnica con identificador {tech.TechniqueId} deben estar entre 0 y 600 (recibido: {tech.DurationMinutes}).");
        }
    }

    /// <summary>
    /// Verifica que todas las técnicas referenciadas existan y estén activas.
    /// Si alguna no cumple, se rechaza el guardado completo informando cuál falla.
    /// </summary>
    private async Task ValidateTechniquesAsync(List<TechniquePracticeInput> techniques)
    {
        if (techniques.Count == 0)
            return;

        var ids = techniques.Select(t => t.TechniqueId).Distinct().ToList();
        var found = await _context.Techniques
            .Where(t => ids.Contains(t.Id))
            .Select(t => new { t.Id, t.Name, t.IsActive })
            .ToListAsync();

        foreach (var id in ids)
        {
            var technique = found.FirstOrDefault(t => t.Id == id);

            if (technique == null)
                throw new InvalidOperationException(
                    $"La técnica con identificador {id} no existe. No se guardó ningún dato del entrenamiento.");

            if (!technique.IsActive)
                throw new InvalidOperationException(
                    $"La técnica '{technique.Name}' (identificador {id}) está inactiva. No se guardó ningún dato del entrenamiento.");
        }
    }

    public async Task<List<TrainingSession>> GetByUserAsync(int userId)
    {
        return await _context.TrainingSessions
            .Include(ts => ts.Practices)
                .ThenInclude(tp => tp.Technique)
            .Where(ts => ts.UserId == userId)
            .OrderByDescending(ts => ts.Date)
            .ToListAsync();
    }

    public async Task<TrainingSession?> GetByIdAsync(int userId, int sessionId)
    {
        return await _context.TrainingSessions
            .Include(ts => ts.Practices)
                .ThenInclude(tp => tp.Technique)
            .FirstOrDefaultAsync(ts => ts.Id == sessionId && ts.UserId == userId);
    }

    public async Task<int> GetTotalCountAsync(int userId)
    {
        return await _context.TrainingSessions
            .CountAsync(ts => ts.UserId == userId);
    }

    public async Task<TrainingSession?> GetLastAsync(int userId)
    {
        return await _context.TrainingSessions
            .Where(ts => ts.UserId == userId)
            .OrderByDescending(ts => ts.Date)
            .FirstOrDefaultAsync();
    }
}
