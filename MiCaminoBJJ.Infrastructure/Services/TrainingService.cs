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

            // Register practices for each technique
            foreach (var tech in input.Techniques)
            {
                // Ensure technique exists and is active
                var techniqueExists = await _context.Techniques
                    .AnyAsync(t => t.Id == tech.TechniqueId && t.IsActive);

                if (!techniqueExists)
                {
                    _logger.LogWarning("Technique {TechniqueId} not found or inactive. Skipping.", tech.TechniqueId);
                    continue;
                }

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

    public async Task<List<TrainingSession>> GetByUserAsync(int userId)
    {
        return await _context.TrainingSessions
            .Include(ts => ts.Practices)
                .ThenInclude(tp => tp.Technique)
            .Where(ts => ts.UserId == userId)
            .OrderByDescending(ts => ts.Date)
            .ToListAsync();
    }

    public async Task<TrainingSession?> GetByIdAsync(int sessionId)
    {
        return await _context.TrainingSessions
            .Include(ts => ts.Practices)
                .ThenInclude(tp => tp.Technique)
            .FirstOrDefaultAsync(ts => ts.Id == sessionId);
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
