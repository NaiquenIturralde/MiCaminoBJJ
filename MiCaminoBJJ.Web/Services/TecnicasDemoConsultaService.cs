using System.Text.Json;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;

namespace MiCaminoBJJ.Web.Services;

/// <summary>
/// Implementación temporal de <see cref="ITecnicasConsultaService"/> (Etapa 1 Web).
///
/// ⚠ DATOS DE DEMOSTRACIÓN EN MEMORIA: catálogo fijo construido en el arranque,
/// con entidades reales del dominio (Technique, Belt, TechniqueCategory, Position,
/// TechniqueAlias) y las MISMAS reglas de búsqueda/normalización que la app
/// móvil (<see cref="TextNormalizer"/>). No toca ninguna base de datos.
///
/// Singleton e inmutable: el catálogo se crea una sola vez y las consultas
/// devuelven copias de lista, nunca modifican las entidades compartidas.
/// </summary>
public sealed class TecnicasDemoConsultaService : ITecnicasConsultaService
{
    private readonly IReadOnlyList<Technique> _tecnicas;
    private readonly IReadOnlyList<Belt> _cinturones;
    private readonly IReadOnlyList<TechniqueCategory> _categorias;
    private readonly IReadOnlyList<Position> _posiciones;

    public TecnicasDemoConsultaService()
    {
        // Cinturones, categorías y posiciones con los mismos nombres del dominio
        // (SeedDataService) para que la demo sea coherente con el resto del sistema.
        var blanco = new Belt { Id = 1, Name = "Blanco", DisplayOrder = 1 };
        var azul = new Belt { Id = 2, Name = "Azul", DisplayOrder = 2 };
        var violeta = new Belt { Id = 3, Name = "Violeta", DisplayOrder = 3 };
        var marron = new Belt { Id = 4, Name = "Marrón", DisplayOrder = 4 };
        var negro = new Belt { Id = 5, Name = "Negro", DisplayOrder = 5 };

        var defensa = new TechniqueCategory { Id = 1, Name = "Defensa", DisplayOrder = 1 };
        var escape = new TechniqueCategory { Id = 2, Name = "Escape", DisplayOrder = 2 };
        var guardia = new TechniqueCategory { Id = 3, Name = "Guardia", DisplayOrder = 3 };
        var sumision = new TechniqueCategory { Id = 4, Name = "Sumisión", DisplayOrder = 4 };
        var control = new TechniqueCategory { Id = 5, Name = "Control", DisplayOrder = 5 };

        var montada = new Position { Id = 1, Name = "Montada", DisplayOrder = 1 };
        var espalda = new Position { Id = 2, Name = "Espalda", DisplayOrder = 2 };
        var guardiaCerrada = new Position { Id = 3, Name = "Guardia cerrada", DisplayOrder = 3 };
        var controlLateral = new Position { Id = 4, Name = "Control lateral", DisplayOrder = 4 };
        var mediaGuardia = new Position { Id = 5, Name = "Media guardia", DisplayOrder = 5 };

        _cinturones = new[] { blanco, azul, violeta, marron, negro };
        _categorias = new[] { defensa, escape, guardia, sumision, control };
        _posiciones = new[] { montada, espalda, guardiaCerrada, controlLateral, mediaGuardia };

        _tecnicas = new[]
        {
            Crear(1, "Escape de montada",
                "Técnica esencial para escapar de la posición de montada. Utiliza el puente y la rodilla para desequilibrar al oponente.",
                "Desequilibrar al oponente desde la montada y recuperar una posición neutral o ventajosa.",
                blanco, escape, montada,
                pasos: new[]
                {
                    "Colocá los codos pegados al cuerpo y protegé el cuello.",
                    "Realizá un puente explosivo hacia un lado para desequilibrar al oponente.",
                    "Cuando el oponente reaccione, usá la rodilla para hacer palanca.",
                    "Escapá a media guardia o guardia cerrada."
                },
                consejos: new[]
                {
                    "El puente debe ser explosivo y sincronizado con la respiración.",
                    "No intentes escapar completamente de una vez; avanzá paso a paso."
                }),

            Crear(2, "Mata león",
                "Estrangulación básica desde la espalda. También conocida como Rear Naked Choke. Es una de las sumisiones más efectivas del BJJ.",
                "Finalizar el combate estrangulando al oponente desde la espalda.",
                blanco, sumision, espalda,
                pasos: new[]
                {
                    "Tomá la espalda del oponente con ambos ganchos adentro.",
                    "Pasá tu brazo dominante por detrás del cuello del oponente.",
                    "Agarrá tu propio bíceps con la mano no dominante.",
                    "Colocá la mano dominante detrás de la cabeza del oponente.",
                    "Apretá los codos y expandí el pecho para estrangular."
                },
                consejos: new[]
                {
                    "No cruces los pies si querés mantener el control.",
                    "Mantené el pegado al cuerpo del oponente."
                },
                alias: new[] { "Rear Naked Choke", "RNC", "Estrangulación trasera" }),

            Crear(3, "Armbar desde guardia cerrada",
                "El Armbar desde guardia cerrada es una técnica básica de sumisión que enseña control, uso de la cadera y aislamiento del brazo del oponente para forzar la rendición.",
                "Aislar el brazo del oponente, romper su postura y aplicar presión en la articulación del codo para lograr la sumisión.",
                blanco, sumision, guardiaCerrada,
                pasos: new[]
                {
                    "Controlá el brazo del oponente y mantené tu guardia cerrada.",
                    "Rompé su postura llevándolo hacia adelante.",
                    "Girá tu cadera hacia el lado del brazo que vas a atacar.",
                    "Pasá una de tus piernas por encima de su cabeza.",
                    "Extendé la cadera y apretá las piernas para finalizar el Armbar."
                },
                consejos: new[]
                {
                    "No tires solamente con los brazos; usá la cadera.",
                    "Mantené las rodillas cerradas para obtener mayor control.",
                    "Aplicá la técnica con control y respeto."
                },
                alias: new[] { "Armbar", "Palanca de brazo" }),

            Crear(4, "Raspado básico",
                "Raspado fundamental desde guardia cerrada. Usa el movimiento de cadera y los brazos para invertir la posición.",
                "Invertir la posición desde la guardia cerrada, llevando al oponente debajo.",
                blanco, guardia, guardiaCerrada,
                pasos: new[]
                {
                    "Abrí la guardia y colocá los pies en la cadera del oponente.",
                    "Agarrá los brazos del oponente y tirá hacia abajo.",
                    "Elevá la cadera y girá hacia un lado.",
                    "Finalizá en posición de montada o control lateral."
                },
                consejos: new[]
                {
                    "El raspado depende del timing, no solo de la fuerza.",
                    "Mantené al oponente desequilibrado hacia adelante."
                },
                alias: new[] { "Sweep básico" }),

            Crear(5, "Posicionamiento de supervivencia con control lateral",
                "Técnica fundamental para sobrevivir bajo control lateral. Aprender a proteger el cuello, mantener los codos pegados y usar la cadera para crear espacio.",
                "Sobrevivir bajo control lateral protegiendo las posiciones críticas y esperando la oportunidad para escapar.",
                blanco, escape, controlLateral,
                pasos: new[]
                {
                    "Mantené los codos pegados al cuerpo para proteger las costillas.",
                    "Colocá la mano del lado libre protegiendo el cuello.",
                    "Usá la cadera para crear espacio y evitar que el oponente avance.",
                    "Buscá media guardia o recuperá la guardia cerrada."
                },
                consejos: new[]
                {
                    "No olvides respirar. El pánico es tu peor enemigo.",
                    "Mantené la calma y esperá el momento adecuado para escapar."
                }),

            Crear(6, "Kimura desde guardia cerrada",
                "Llave de hombro aplicada desde la guardia cerrada controlando la muñeca del oponente y girando el cuerpo para alejar el codo.",
                "Controlar la muñeca, alejar el codo del oponente y girar para aplicar presión sobre el hombro.",
                azul, sumision, guardiaCerrada,
                pasos: new[]
                {
                    "Controlá la muñeca del lado que vas a atacar.",
                    "Pasá tu brazo por debajo del brazo del oponente y agarrá tu propia muñeca.",
                    "Soltá la guardia y girá hacia el lado del ataque.",
                    "Levantá la cadera y girá al oponente para aplicar la llave."
                },
                consejos: new[]
                {
                    "Mantené el codo del oponente pegado a tu pecho.",
                    "Aplicá presión de a poco y con control."
                },
                alias: new[] { "Kimura", "Llave de hombro" }),

            Crear(7, "Pass de corte de rodilla",
                "Pasaje de guardia por corte de rodilla: se divide la guardia con la pierna de adentro y se consolida el control lateral.",
                "Superar la guardia del oponente cortando con la rodilla y llegando a control lateral.",
                azul, control, controlLateral,
                pasos: new[]
                {
                    "Colocá tus caderas cerca del oponente y poné el pie de apoyo afuera.",
                    "Agarrá la cintura o la colleta para fijar el torso.",
                    "Cortá hacia el lado de la pierna de adentro con tu rodilla.",
                    "Terminá con ambos brazos en control y las caderas bajas."
                },
                consejos: new[]
                {
                    "No cortes sin controlar la cintura.",
                    "Mantené la cabeza sobre la del oponente para no perder el ángulo."
                },
                alias: new[] { "Knee cut", "Corte de rodilla" }),

            Crear(8, "Triángulo desde guardia cerrada",
                "Estrangulación con las piernas desde la guardia cerrada que aísla un brazo y el cuello del oponente.",
                "Aislar un brazo y el cuello cruzando las piernas para aplicar la estrangulación.",
                azul, sumision, guardiaCerrada,
                pasos: new[]
                {
                    "Sacá un brazo del oponente hacia adentro y controlá la cabeza.",
                    "Colocá la pierna del lado del brazo atrapado sobre la nuca.",
                    "Cruzá los tobillos y ajustá el ángulo girando el cuerpo.",
                    "Tirá de la cabeza y apretá las rodillas para finalizar."
                },
                consejos: new[]
                {
                    "Cerrá el ángulo antes de apretar.",
                    "No dejes que el oponente se levante sobre vos."
                },
                alias: new[] { "Triángulo" }),

            Crear(9, "Puente y giro desde control lateral",
                "Escape intermedio de control lateral que combina el puente para romper el balance con el giro hacia la rodilla de adentro.",
                "Romper el balance del oponente con un puente y girar para recuperar la media guardia.",
                violeta, escape, controlLateral,
                pasos: null,
                consejos: null),

            Crear(10, "Defensa de kimura en control lateral",
                "Defensa contra la llave de hombro desde control lateral: se bloquea la mano, se gira el cuerpo y se recupera la posición.",
                "Impedir que el oponente aísle la muñeca y girar para recuperar una posición segura.",
                azul, defensa, controlLateral,
                pasos: new[]
                {
                    "Enganchá tu mano en la pierna u otra parte estable para bloquear.",
                    "Girá hacia el lado amenazado antes de que aíslen la muñeca.",
                    "Recuperá la media guardia o quedate de costado."
                },
                consejos: new[]
                {
                    "Nunca dejes la mano suelta cerca del cinturón.",
                    "Reaccioná antes de que cierren el ángulo."
                },
                alias: new[] { "Defensa Kimura" }),

            Crear(11, "Estrangulación con collar en control lateral",
                "Estrangulación con gi desde control lateral utilizando la solapa del oponente con ambas manos cruzadas.",
                "Aplicar presión sobre las arterias con la solapa manteniendo el control del cuerpo.",
                marron, sumision, controlLateral,
                pasos: new[]
                {
                    "Conseguí la solapa con la mano de abajo.",
                    "Pasá la segunda mano por encima de la cabeza del oponente.",
                    "Cruzá las manos y llevá las caderas hacia atrás.",
                    "Mantené el pecho bajo para sostener el control."
                },
                consejos: new[]
                {
                    "Si no llegás a la solapa, mejor mantenga el control.",
                    "Aplicá la estrangulación de a poco."
                },
                alias: new[] { "Collar choke" })
        };
    }

    public bool EsFuenteDeDemostracion => true;

    public Task<IReadOnlyList<Technique>> SearchAsync(TechniqueSearchFilter filter)
    {
        filter ??= new TechniqueSearchFilter();

        IEnumerable<Technique> query = _tecnicas.Where(t => t.IsActive);

        // Misma semántica que TechniqueSearchService (SQL):
        // término normalizado vacío => sin filtro de texto; si no, match por
        // nombre normalizado, alias normalizado o descripción/consejos literal
        // (insensible a mayúsculas).
        var searchTerm = filter.SearchTerm;
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalized = TextNormalizer.Normalize(searchTerm);
            if (normalized.Length > 0)
            {
                var literal = searchTerm.Trim();
                query = query.Where(t =>
                    TextNormalizer.Contains(t.Name, searchTerm) ||
                    t.Aliases.Any(a => TextNormalizer.Contains(a.Name, searchTerm)) ||
                    (!string.IsNullOrEmpty(t.Description) &&
                     t.Description.Contains(literal, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(t.Tips) &&
                     t.Tips.Contains(literal, StringComparison.OrdinalIgnoreCase)));
            }
        }

        if (filter.BeltId.HasValue)
            query = query.Where(t => t.BeltId == filter.BeltId.Value);

        if (filter.CategoryId.HasValue)
            query = query.Where(t => t.CategoryId == filter.CategoryId.Value);

        if (filter.PositionId.HasValue)
            query = query.Where(t => t.PositionId == filter.PositionId.Value);

        IReadOnlyList<Technique> result = query
            .OrderBy(t => t.Belt.DisplayOrder)
            .ThenBy(t => t.Name, StringComparer.CurrentCulture)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<Technique?> GetByIdAsync(int id)
    {
        // Búsqueda por identificador sin filtrar IsActive: el controlador decide
        // si una técnica inactiva debe responder 404 (misma regla que el detalle).
        Technique? technique = _tecnicas.FirstOrDefault(t => t.Id == id);
        return Task.FromResult(technique);
    }

    public Task<Technique?> GetByNombreOAliasExactoAsync(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return Task.FromResult<Technique?>(null);

        var normalizado = TextNormalizer.Normalize(nombre);
        if (normalizado.Length == 0)
            return Task.FromResult<Technique?>(null);

        // Misma semántica que la implementación SQL Server: nombre O alias
        // exacto (TextNormalizer), SIN filtrar IsActive.
        Technique? encontrada = _tecnicas.FirstOrDefault(t =>
            TextNormalizer.Normalize(t.NameNormalized ?? t.Name) == normalizado ||
            t.Aliases.Any(a => TextNormalizer.Normalize(a.NameNormalized ?? a.Name) == normalizado));

        return Task.FromResult(encontrada);
    }

    public Task<IReadOnlyList<Belt>> GetBeltsAsync()
    {
        IReadOnlyList<Belt> belts = _cinturones.OrderBy(b => b.DisplayOrder).ToList();
        return Task.FromResult(belts);
    }

    public Task<IReadOnlyList<TechniqueCategory>> GetCategoriesAsync()
    {
        IReadOnlyList<TechniqueCategory> categories = _categorias.OrderBy(c => c.DisplayOrder).ToList();
        return Task.FromResult(categories);
    }

    public Task<IReadOnlyList<Position>> GetPositionsAsync()
    {
        IReadOnlyList<Position> positions = _posiciones.OrderBy(p => p.DisplayOrder).ToList();
        return Task.FromResult(positions);
    }

    private static Technique Crear(
        int id,
        string nombre,
        string descripcion,
        string objetivo,
        Belt cinturon,
        TechniqueCategory categoria,
        Position posicion,
        string[]? pasos,
        string[]? consejos,
        string[]? alias = null)
    {
        var technique = new Technique
        {
            Id = id,
            Name = nombre,
            // Misma regla que TechniqueService/SeedDataService: NameNormalized al persistir.
            NameNormalized = TextNormalizer.Normalize(nombre),
            Description = descripcion,
            Objective = objetivo,
            Steps = pasos is null ? null : JsonSerializer.Serialize(pasos),
            Tips = consejos is null ? null : JsonSerializer.Serialize(consejos),
            BeltId = cinturon.Id,
            CategoryId = categoria.Id,
            PositionId = posicion.Id,
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Belt = cinturon,
            Category = categoria,
            Position = posicion
        };

        if (alias is not null)
        {
            foreach (var name in alias)
            {
                technique.Aliases.Add(new TechniqueAlias
                {
                    Id = technique.Aliases.Count + 1,
                    TechniqueId = id,
                    Name = name,
                    NameNormalized = TextNormalizer.Normalize(name),
                    Technique = technique
                });
            }
        }

        return technique;
    }
}
