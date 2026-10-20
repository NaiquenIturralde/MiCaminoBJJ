using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;

namespace MiCaminoBJJ.Core.Interfaces;

public interface ITechniquePracticeService
{
    Task<TechniquePractice> RegisterPracticeAsync(int userId, int techniqueId, int? trainingSessionId,
        int repetitions, int durationMinutes, ConfidenceLevel confidence, string? notes, DateTime practicedAt);
    Task<List<TechniquePractice>> GetByTechniqueAsync(int userId, int techniqueId);
    Task<List<TechniquePractice>> GetByTrainingSessionAsync(int trainingSessionId);
    Task<int> GetTotalRepetitionsAsync(int userId, int techniqueId);
    Task<int> GetTotalSessionsAsync(int userId, int techniqueId);
    Task<int> GetTotalMinutesAsync(int userId, int techniqueId);
    Task<DateTime?> GetLastPracticedAtAsync(int userId, int techniqueId);
}
