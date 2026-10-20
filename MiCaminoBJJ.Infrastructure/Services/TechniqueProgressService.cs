using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

public class TechniqueProgressService : ITechniqueProgressService
{
    private readonly AppDbContext _context;
    private readonly ITechniqueProgressCalculator _calculator;
    private readonly ITechniquePracticeService _practiceService;
    private readonly ILogger<TechniqueProgressService> _logger;

    public TechniqueProgressService(
        AppDbContext context,
        ITechniqueProgressCalculator calculator,
        ITechniquePracticeService practiceService,
        ILogger<TechniqueProgressService> logger)
    {
        _context = context;
        _calculator = calculator;
        _practiceService = practiceService;
        _logger = logger;
    }

    public async Task<UserTechniqueProgress?> GetAsync(int userId, int techniqueId)
    {
        return await _context.UserTechniqueProgress
            .FirstOrDefaultAsync(tp => tp.UserId == userId && tp.TechniqueId == techniqueId);
    }

    public async Task<UserTechniqueProgress> GetOrCreateAsync(int userId, int techniqueId)
    {
        var progress = await GetAsync(userId, techniqueId);
        if (progress != null)
            return progress;

        progress = new UserTechniqueProgress
        {
            UserId = userId,
            TechniqueId = techniqueId,
            ProgressState = ProgressState.NoIniciada
        };

        _context.UserTechniqueProgress.Add(progress);
        await _context.SaveChangesAsync();
        return progress;
    }

    public async Task UpdateStateAsync(int userId, int techniqueId, ProgressState newState)
    {
        var progress = await GetOrCreateAsync(userId, techniqueId);
        progress.ProgressState = newState;
        await _context.SaveChangesAsync();
    }

    public async Task MarkAsDominatedAsync(int userId, int techniqueId)
    {
        var progress = await GetOrCreateAsync(userId, techniqueId);
        progress.ProgressState = ProgressState.Dominada;
        progress.DominatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Technique {TechniqueId} marked as dominated by user {UserId}", techniqueId, userId);
    }

    public async Task<double> CalculatePercentageAsync(int userId, int techniqueId)
    {
        var progress = await GetAsync(userId, techniqueId);
        var isDominated = progress?.ProgressState == ProgressState.Dominada;

        var sessions = await _practiceService.GetTotalSessionsAsync(userId, techniqueId);
        var reps = await _practiceService.GetTotalRepetitionsAsync(userId, techniqueId);
        var minutes = await _practiceService.GetTotalMinutesAsync(userId, techniqueId);

        // Calculate average confidence
        var practices = await _practiceService.GetByTechniqueAsync(userId, techniqueId);
        var avgConfidence = practices.Any()
            ? practices.Average(p => (int)p.ConfidenceLevel)
            : 0.0;

        return _calculator.Calculate(sessions, reps, minutes, avgConfidence, isDominated);
    }

    public async Task<List<UserTechniqueProgress>> GetByUserAsync(int userId)
    {
        return await _context.UserTechniqueProgress
            .Include(tp => tp.Technique)
                .ThenInclude(t => t.Belt)
            .Where(tp => tp.UserId == userId)
            .ToListAsync();
    }
}
