using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Infrastructure.Data;
using MiCaminoBJJ.Infrastructure.Services;

namespace MiCaminoBJJ.Infrastructure.Data.Seed;

public class SeedDataService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<SeedDataService> _logger;
    private readonly IConfiguration? _configuration;

    public SeedDataService(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        ILogger<SeedDataService> logger,
        IConfiguration? configuration = null)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SeedAsync()
    {
        if (await _context.Belts.AnyAsync())
        {
            _logger.LogInformation("Database already seeded. Skipping.");
            return;
        }

        _logger.LogInformation("Starting database seed...");

        // Belts
        var belts = new List<Belt>
        {
            new() { Name = "Blanco", DisplayOrder = 1, Description = "El comienzo del camino. Aprender a sobrevivir." },
            new() { Name = "Azul", DisplayOrder = 2, Description = "Fundamentos sólidos y primeras conexiones." },
            new() { Name = "Violeta", DisplayOrder = 3, Description = "Madurez técnica y desarrollo de juego propio." },
            new() { Name = "Marrón", DisplayOrder = 4, Description = "Refinamiento y enseñanza." },
            new() { Name = "Negro", DisplayOrder = 5, Description = "Maestría y liderazgo." }
        };
        _context.Belts.AddRange(belts);
        await _context.SaveChangesAsync();

        // Categories
        var categories = new List<TechniqueCategory>
        {
            new() { Name = "Defensa", DisplayOrder = 1, Description = "Técnicas de defensa y protección." },
            new() { Name = "Escape", DisplayOrder = 2, Description = "Técnicas para escapar de posiciones desfavorables." },
            new() { Name = "Guardia", DisplayOrder = 3, Description = "Técnicas de guardia y barridos." },
            new() { Name = "Sumisión", DisplayOrder = 4, Description = "Finalizaciones y estrangulaciones." },
            new() { Name = "Control", DisplayOrder = 5, Description = "Técnicas de control y pasaje de guardia." }
        };
        _context.TechniqueCategories.AddRange(categories);
        await _context.SaveChangesAsync();

        // Positions
        var positions = new List<Position>
        {
            new() { Name = "Montada", DisplayOrder = 1, Description = "Montada con control." },
            new() { Name = "Espalda", DisplayOrder = 2, Description = "Control de espalda." },
            new() { Name = "Guardia cerrada", DisplayOrder = 3, Description = "Guardia cerrada." },
            new() { Name = "Control lateral", DisplayOrder = 4, Description = "Control lateral (100 kilos)." },
            new() { Name = "Media guardia", DisplayOrder = 5, Description = "Media guardia." }
        };
        _context.Positions.AddRange(positions);
        await _context.SaveChangesAsync();

        // Seed demo admin user (only in development).
        // La contraseña NO es un literal en el código: la resuelve
        // SeedAdminPassword desde la configuración ("AdminDev:Password") y, si no
        // hay ninguna configurada, genera una contraseña aleatoria. Así el
        // repositorio no publica ninguna credencial fija y adivinable.
        var adminPasswordHash = _passwordHasher.HashPassword(
            SeedAdminPassword.Resolve(_configuration),
            out var adminPasswordSalt);
        var admin = new User
        {
            Name = "Administrador",
            Email = "admin@micaminobjj.com",
            PasswordHash = adminPasswordHash,
            PasswordSalt = adminPasswordSalt,
            BeltRank = "Negro",
            Academy = "Mi Camino BJJ",
            Role = UserRole.Administrador
        };
        _context.Users.Add(admin);
        await _context.SaveChangesAsync();

        // Techniques
        var whiteBelt = belts[0];
        var escapeCat = categories[1];
        var submissionCat = categories[3];
        var guardCat = categories[2];
        var sideControl = positions[3];
        var closedGuard = positions[2];
        var mount = positions[0];
        var back = positions[1];
        var halfGuard = positions[4];

        var techniques = new List<Technique>
        {
            new()
            {
                Name = "Posicionamiento de supervivencia con control lateral",
                Description = "Técnica fundamental para sobrevivir bajo control lateral. Aprender a proteger el cuello, mantener los codos pegados y usar la cadera para crear espacio.",
                Objective = "Sobrevivir bajo control lateral protegiendo las posiciones críticas y esperando la oportunidad para escapar.",
                Steps = "[\"Mantené los codos pegados al cuerpo para proteger las costillas.\",\"Colocá la mano del lado libre protegiendo el cuello.\",\"Usá la cadera para crear espacio y evitar que el oponente avance.\",\"Buscá media guardia o recuperá la guardia cerrada.\"]",
                Tips = "[\"No olvides respirar. El pánico es tu peor enemigo.\",\"Mantené la calma y esperá el momento adecuado para escapar.\"]",
                BeltId = whiteBelt.Id,
                CategoryId = escapeCat.Id,
                PositionId = sideControl.Id,
                IsActive = true
            },
            new()
            {
                Name = "Armbar desde guardia cerrada",
                Description = "El Armbar desde guardia cerrada es una técnica básica de sumisión que enseña control, uso de la cadera y aislamiento del brazo del oponente para forzar la rendición.",
                Objective = "Aislar el brazo del oponente, romper su postura y aplicar presión en la articulación del codo para lograr la sumisión.",
                Steps = "[\"Controlá el brazo del oponente y mantené tu guardia cerrada.\",\"Rompé su postura llevándolo hacia adelante.\",\"Girá tu cadera hacia el lado del brazo que vas a atacar.\",\"Pasá una de tus piernas por encima de su cabeza.\",\"Extendé la cadera y apretá las piernas para finalizar el Armbar.\"]",
                Tips = "[\"No tires solamente con los brazos; usá la cadera.\",\"Mantené las rodillas cerradas para obtener mayor control.\",\"Asegurate de romper la postura antes de finalizar.\",\"Aplicá la técnica con control y respeto.\"]",
                BeltId = whiteBelt.Id,
                CategoryId = submissionCat.Id,
                PositionId = closedGuard.Id,
                IsActive = true
            },
            new()
            {
                Name = "Escape de montada",
                Description = "Técnica esencial para escapar de la posición de montada. Utiliza el puente y la rodilla para desequilibrar al oponente.",
                Objective = "Desequilibrar al oponente desde la montada y recuperar una posición neutral o ventajosa.",
                Steps = "[\"Colocá los codos pegados al cuerpo y protegé el cuello.\",\"Realizá un puente explosivo hacia un lado para desequilibrar al oponente.\",\"Cuando el oponente reaccione, usá la rodilla para hacer palanca.\",\"Escapá a media guardia o guardia cerrada.\"]",
                Tips = "[\"El puente debe ser explosivo y sincronizado con la respiración.\",\"No intentes escapar completamente de una vez; avanzá paso a paso.\"]",
                BeltId = whiteBelt.Id,
                CategoryId = escapeCat.Id,
                PositionId = mount.Id,
                IsActive = true
            },
            new()
            {
                Name = "Mata león",
                Description = "Estrangulación básica desde la espalda. También conocida como Rear Naked Choke. Es una de las sumisiones más efectivas del BJJ.",
                Objective = "Finalizar el combate estrangulando al oponente desde la espalda.",
                Steps = "[\"Tomá la espalda del oponente con ambos ganchos adentro.\",\"Pasá tu brazo dominante por detrás del cuello del oponente.\",\"Agarrá tu propio bíceps con la mano no dominante.\",\"Colocá la mano dominante detrás de la cabeza del oponente.\",\"Apretá los codos y expandí el pecho para estrangular.\"]",
                Tips = "[\"No cruces los pies si querés mantener el control.\",\"Mantené el pegado al cuerpo del oponente.\"]",
                BeltId = whiteBelt.Id,
                CategoryId = submissionCat.Id,
                PositionId = back.Id,
                IsActive = true
            },
            new()
            {
                Name = "Raspado básico",
                Description = "Raspado fundamental desde guardia cerrada. Usa el movimiento de cadera y los brazos para invertir la posición.",
                Objective = "Invertir la posición desde la guardia cerrada, llevando al oponente debajo.",
                Steps = "[\"Abrí la guardia y colocá los pies en la cadera del oponente.\",\"Agarrá los brazos del oponente y tirá hacia abajo.\",\"Elevá la cadera y girá hacia un lado.\",\"Finalizá en posición de montada o control lateral.\"]",
                Tips = "[\"El raspado depende del timing, no solo de la fuerza.\",\"Mantené al oponente desequilibrado hacia adelante.\"]",
                BeltId = whiteBelt.Id,
                CategoryId = guardCat.Id,
                PositionId = closedGuard.Id,
                IsActive = true
            }
        };
        // Misma regla que TechniqueService/TextNormalizer: las técnicas del seed
        // quedan consultables por búsqueda normalizada sin depender solo del backfill.
        foreach (var technique in techniques)
            technique.NameNormalized = TextNormalizer.Normalize(technique.Name);

        _context.Techniques.AddRange(techniques);
        await _context.SaveChangesAsync();

        // Official media for techniques (packaged resources - placeholder refs)
        var adminUser = await _context.Users.FirstAsync(u => u.Email == "admin@micaminobjj.com");
        var mediaAssets = new List<MediaAsset>();
        var techniqueMediaEntries = new List<TechniqueMedia>();
        var displayOrder = 0;

        foreach (var technique in techniques)
        {
            displayOrder++;
            var media = new MediaAsset
            {
                UploadedByUserId = adminUser.Id,
                MediaType = MediaType.Image,
                SourceType = MediaSourceType.PackagedResource,
                LocalPath = $"media/official/techniques/technique_{technique.Id}.png",
                Title = $"Imagen de {technique.Name}",
                SyncStatus = SyncStatus.LocalOnly
            };
            mediaAssets.Add(media);
            techniqueMediaEntries.Add(new TechniqueMedia
            {
                TechniqueId = technique.Id,
                MediaAssetId = 0, // Will be set after save
                IsCover = true,
                DisplayOrder = 1
            });
        }

        _context.MediaAssets.AddRange(mediaAssets);
        await _context.SaveChangesAsync();

        // Link media to techniques
        for (int i = 0; i < techniques.Count; i++)
        {
            techniqueMediaEntries[i].MediaAssetId = mediaAssets[i].Id;
        }
        _context.TechniqueMedia.AddRange(techniqueMediaEntries);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Database seed completed successfully.");
    }
}
