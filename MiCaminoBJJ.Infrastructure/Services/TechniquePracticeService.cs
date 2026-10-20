using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

public class TechniquePracticeService : ITechniquePracticeService
{
    private readonly AppDbContext _context;
    private readonly ILogger<TechniquePracticeService> _logger;

    public TechniquePracticeService(AppDbContext context, ILogger<TechniquePracticeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TechniquePractice> RegisterPracticeAsync(int userId, int techniqueId, int? trainingSessionId,
        int repetitions, int durationMinutes, ConfidenceLevel confidence, string? notes, DateTime practicedAt)
    {
        var practice = new TechniquePractice
        {
            UserId = userId,
            TechniqueId = techniqueId,
            TrainingSessionId = trainingSessionId,
            PracticedAt = practicedAt,
            Repetitions = repetitions,
            DurationMinutes = durationMinutes,
            ConfidenceLevel = confidence,
            Notes = notes
        };

        _context.TechniquePractices.Add(practice);
        await _context.SaveChangesAsync();

        _logger.LogDebug("Practice registered: User={UserId}, Technique={TechniqueId}, Reps={Reps}",
            userId, techniqueId, repetitions);

        return practice;
    }

    public async Task<List<TechniquePractice>> GetByTechniqueAsync(int userId, int techniqueId)
    {
        return await _context.TechniquePractices
            .Where(tp => tp.UserId == userId && tp.TechniqueId == techniqueId)
            .OrderByDescending(tp => tp.PracticedAt)
            .ToListAsync();
    }

    public async Task<List<TechniquePractice>> GetByTrainingSessionAsync(int trainingSessionId)
    {
        return await _context.TechniquePractices
            .Where(tp => tp.TrainingSessionId == trainingSessionId)
            .ToListAsync();
    }

    public async Task<int> GetTotalRepetitionsAsync(int userId, int techniqueId)
    {
        return await _context.TechniquePractices
            .Where(tp => tp.UserId == userId && tp.TechniqueId == techniqueId)
            .SumAsync(tp => tp.Repetitions);
    }

    public async Task<int> GetTotalSessionsAsync(int userId, int techniqueId)
    {
        return await _context.TechniquePractices
            .CountAsync(tp => tp.UserId == userId && tp.TechniqueId == techniqueId);
    }

    public async Task<int> GetTotalMinutesAsync(int userId, int techniqueId)
    {
        return await _context.TechniquePractices
            .Where(tp => tp.UserId == userId && tp.TechniqueId == techniqueId)
            .SumAsync(tp => tp.DurationMinutes);
    }

    public async Task<DateTime?> GetLastPracticedAtAsync(int userId, int techniqueId)
    {
        return await _context.TechniquePractices
            .Where(tp => tp.UserId == userId && tp.TechniqueId == techniqueId)
            .OrderByDescending(tp => tp.PracticedAt)
            .Select(tp => (DateTime?)tp.PracticedAt)
            .FirstOrDefaultAsync();
    }
}
