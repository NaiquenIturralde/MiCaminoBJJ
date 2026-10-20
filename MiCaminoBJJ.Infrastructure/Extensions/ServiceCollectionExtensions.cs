using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Infrastructure.Data;
using MiCaminoBJJ.Infrastructure.Data.Seed;
using MiCaminoBJJ.Infrastructure.Services;

namespace MiCaminoBJJ.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string dbPath)
    {
        // EF Core
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Domain services
        services.AddSingleton<ITechniqueProgressCalculator, TechniqueProgressCalculator>();
        services.AddSingleton<IExternalMediaParser, ExternalMediaParser>();

        // Infrastructure services
        // ISessionService is registered in the App project (SecureSessionService)
        services.AddScoped<IPasswordHasher, PasswordService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITechniqueService, TechniqueService>();
        services.AddScoped<ITechniqueSearchService, TechniqueSearchService>();
        services.AddScoped<ISavedTechniqueService, SavedTechniqueService>();
        services.AddScoped<ITechniquePracticeService, TechniquePracticeService>();
        services.AddScoped<ITechniqueProgressService, TechniqueProgressService>();
        services.AddScoped<ITrainingService, TrainingService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IAchievementService, AchievementService>();
        services.AddScoped<IBeltService, BeltService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IPositionService, PositionService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IMediaStorageService, MediaStorageService>();

        // Database
        services.AddScoped<SeedDataService>();
        services.AddScoped<DatabaseInitializer>();

        return services;
    }
}
