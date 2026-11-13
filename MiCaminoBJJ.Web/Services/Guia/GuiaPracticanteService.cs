using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Localization;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Web.Localization;

namespace MiCaminoBJJ.Web.Services.Guia;

/// <summary>
/// Implementación del <see cref="IGuiaPracticanteService"/>.
///
/// Dos rutas, con etiqueta honesta:
/// 1. IA generativa: SOLO si el proveedor está configurado (User Secrets) y
///    responde dentro del timeout. El prompt incluye el catálogo completo y
///    las reglas de seguridad; jamás datos privados.
/// 2. Catálogo: respuesta armada localmente con las técnicas de SQL Server
///    (nombres, alias, cinturón, categoría, posición, objetivo, pasos y
///    relacionadas) más conceptos generales de BJJ. Es la ruta por defecto y
///    también el fallback ante fallo/timeout del proveedor.
///
/// Fuente de datos: <see cref="ITecnicasConsultaService"/> (AsNoTracking, solo
/// técnicas activas). El ÚNICO dato de perfil que recibe es el nivel de cinturón.
/// </summary>
public sealed class GuiaPracticanteService : IGuiaPracticanteService
{
    /// <summary>Nota de seguridad agregada a toda explicación de movimientos.</summary>
    private const string NotaSeguridadClave = "Guia.Nota.Seguridad";

    private static readonly string[] SugerenciasFijasClaves =
    [
        "Guia.Sugerencia.Guardia",
        "Guia.Sugerencia.CinturonBlanco",
        "Guia.Sugerencia.Mejorar"
    ];

    private readonly ITecnicasConsultaService _catalogo;
    private readonly IProveedorIaCliente _proveedor;
    private readonly GuiaOpciones _opciones;
    private readonly IStringLocalizer<UiResources> _localizador;
    private readonly ILogger<GuiaPracticanteService> _logger;

    public GuiaPracticanteService(
        ITecnicasConsultaService catalogo,
        IProveedorIaCliente proveedor,
        GuiaOpciones opciones,
        IStringLocalizer<UiResources> localizador,
        ILogger<GuiaPracticanteService> logger)
    {
        _catalogo = catalogo ?? throw new ArgumentNullException(nameof(catalogo));
        _proveedor = proveedor ?? throw new ArgumentNullException(nameof(proveedor));
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
        _localizador = localizador ?? throw new ArgumentNullException(nameof(localizador));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Modo REAL disponible (nunca se presenta catálogo como IA).</summary>
    public string ModoDescripcion => _opciones.EstaConfigurada()
        ? _localizador["Guia.Modo.Ia"]
        : _localizador["Guia.Modo.Catalogo"];

    public async Task<GuiaRespuesta> ResponderAsync(GuiaConsulta consulta, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var texto = (consulta.Texto ?? string.Empty).Trim();
        if (texto.Length > GuiaLimites.MaxCaracteresPregunta)
            texto = texto[..GuiaLimites.MaxCaracteresPregunta];

        var nivel = string.IsNullOrWhiteSpace(consulta.NivelCinturon)
            ? null
            : consulta.NivelCinturon!.Trim();

        IReadOnlyList<Technique> tecnicas;
        try
        {
            tecnicas = await _catalogo.SearchAsync(new TechniqueSearchFilter());
        }
        catch (Exception ex)
        {
            // Falla de la base: mensaje claro, sin inventar datos.
            _logger.LogError(ex, "Guía: no se pudo consultar la biblioteca de técnicas.");
            return new GuiaRespuesta(
                _localizador["Guia.Error.Consulta"],
                GuiaModo.Catalogo,
                SugerenciasFijas,
                EsError: true);
        }

        // ── Ruta 1: IA generativa (solo si está configurada) ──────────────
        if (_opciones.EstaConfigurada())
        {
            try
            {
                var peticion = ConstruirPeticion(texto, tecnicas, nivel);
                var generado = (await _proveedor.CompletarAsync(peticion, cancelacion)).Trim();

                if (generado.Length > GuiaLimites.MaxCaracteresRespuesta)
                    generado = generado[..GuiaLimites.MaxCaracteresRespuesta];

                if (!string.IsNullOrWhiteSpace(generado))
                {
                    return new GuiaRespuesta(
                        generado,
                        GuiaModo.InteligenciaArtificial,
                        SugerenciasPara(tecnicas, nivel));
                }
            }
            catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
            {
                // Timeout del proveedor: fallback a catálogo, visible para el usuario.
                _logger.LogWarning("Guía: timeout del proveedor de IA; se responde con catálogo.");
                return (await ResponderCatalogoAsync(texto, tecnicas, nivel)) with
                {
                    Nota = _localizador["Guia.Nota.Timeout"]
                };
            }
            catch (Exception ex)
            {
                // Cualquier fallo del proveedor NUNCA interrumpe la consulta.
                _logger.LogWarning(ex, "Guía: fallo del proveedor de IA; se responde con catálogo.");
                return (await ResponderCatalogoAsync(texto, tecnicas, nivel)) with
                {
                    Nota = _localizador["Guia.Nota.Fallo"]
                };
            }
        }

        // ── Ruta 2: guía basada en catálogo (por defecto) ─────────────────
        return await ResponderCatalogoAsync(texto, tecnicas, nivel);
    }

    // ── Catálogo ──────────────────────────────────────────────────────────

    private async Task<GuiaRespuesta> ResponderCatalogoAsync(
        string texto, IReadOnlyList<Technique> tecnicas, string? nivel)
    {
        var normalizado = TextNormalizer.Normalize(texto);
        var sugerencias = SugerenciasPara(tecnicas, nivel);

        GuiaRespuesta Nueva(string cuerpo) => new(cuerpo, GuiaModo.Catalogo, sugerencias);

        // 1) Nombre o alias EXACTO de una técnica registrada.
        var exacta = tecnicas.FirstOrDefault(t =>
            TextNormalizer.AreEqual(t.Name, texto)
            || t.Aliases.Any(a => TextNormalizer.AreEqual(a.Name, texto)));
        if (exacta is not null)
            return Nueva(ExplicarTecnica(exacta, tecnicas));

        // 2) Listado por cinturón ("Mostrame técnicas de cinturón blanco").
        var cinturon = tecnicas
            .Select(t => t.Belt)
            .FirstOrDefault(b => normalizado.Contains(TextNormalizer.Normalize(b.Name)));
        if (cinturon is not null)
        {
            var delCinturon = tecnicas.Where(t => t.BeltId == cinturon.Id).ToList();
            if (delCinturon.Count > 0)
                return Nueva(Listar($"Técnicas de cinturón {cinturon.Name} registradas:", delCinturon));
        }

        // 3) Conceptos generales de BJJ (educativos, sin inventar de la base).
        var concepto = Conceptos.FirstOrDefault(c => c.Claves.Any(k => normalizado.Contains(k)));
        if (concepto is not null)
            return Nueva(ExplicarConcepto(concepto, tecnicas));

        // 4) Dudas sobre el aprendizaje.
        if (Contiene(normalizado, "MEJORAR", "APRENDER", "ESTUDIAR", "REPASAR", "PROGRESAR", "APRENDIZAJE"))
            return Nueva(RespuestaAprendizaje(tecnicas, nivel));

        // 5) Búsqueda libre por nombre/alias/descripción (mismo servicio que la biblioteca).
        IReadOnlyList<Technique> encontradas;
        try
        {
            encontradas = await _catalogo.SearchAsync(new TechniqueSearchFilter { SearchTerm = texto });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Guía: falló la búsqueda en la biblioteca.");
            encontradas = Array.Empty<Technique>();
        }

        if (encontradas.Count == 1)
            return Nueva(ExplicarTecnica(encontradas[0], tecnicas));
        if (encontradas.Count > 1)
            return Nueva(Listar("Encontré estas técnicas en la biblioteca:", encontradas));

        // 6) Pide explicar algo que NO está registrado: se dice tal cual.
        if (PideExplicacion(normalizado))
        {
            return Nueva(
                $"No encontré \"{texto.Trim()}\" en la biblioteca de Mi Camino BJJ: no está registrada, "
                + "así que no puedo explicarla como si existiera en el sistema. "
                + "Mirá el listado completo en la biblioteca o preguntame por otra técnica.");
        }

        // 7) Ayuda general.
        return Nueva(
            "Puedo ayudarte con las técnicas registradas en Mi Camino BJJ:\n"
            + "• Pedime una técnica por nombre o por el nombre alternativo con el que la conozcas "
            + "(ej.: \"Kimura desde guardia cerrada\").\n"
            + "• Pedime un listado por cinturón (ej.: \"Mostrame técnicas de cinturón blanco\").\n"
            + "• Preguntame por conceptos (guardia, montada, espalda, sumisión, cinturones).\n"
            + "• Preguntame cómo mejorar tu aprendizaje.\n\n"
            + "También podés consultar el detalle y los pasos de cada técnica en la biblioteca.");
    }

    private string ExplicarTecnica(Technique t, IReadOnlyList<Technique> todas)
    {
        var sb = new StringBuilder();
        sb.Append(t.Name);

        var alias = t.Aliases?.Select(a => a.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? new List<string>();
        if (alias.Count > 0)
            sb.Append(" (alias: ").Append(string.Join(", ", alias)).Append(')');
        sb.AppendLine();

        sb.Append("Cinturón: ").Append(t.Belt?.Name ?? "—")
          .Append(" · Categoría: ").Append(t.Category?.Name ?? "—")
          .Append(" · Posición: ").Append(t.Position?.Name ?? "—")
          .AppendLine();

        if (!string.IsNullOrWhiteSpace(t.Objective))
            sb.AppendLine().Append("Objetivo: ").Append(t.Objective.Trim());

        if (!string.IsNullOrWhiteSpace(t.Description))
            sb.AppendLine().AppendLine().Append(t.Description.Trim());

        var pasos = ParsearLista(t.Steps);
        if (pasos.Count > 0)
        {
            sb.AppendLine().AppendLine().AppendLine("Pasos de ejecución:");
            for (var i = 0; i < pasos.Count; i++)
                sb.Append(i + 1).Append(". ").AppendLine(pasos[i]);
        }

        var relacionadas = Relacionadas(t, todas);
        if (relacionadas.Count > 0)
        {
            sb.AppendLine().AppendLine()
              .Append("Para repasar después: ").Append(string.Join(", ", relacionadas)).Append('.');
        }

        sb.AppendLine().AppendLine().Append(_localizador[NotaSeguridadClave]);
        return sb.ToString().Trim();
    }

    private string Listar(string titulo, IEnumerable<Technique> tecnicas)
    {
        var sb = new StringBuilder(titulo);
        foreach (var t in tecnicas.OrderBy(t => t.Name, StringComparer.CurrentCulture))
        {
            sb.AppendLine()
              .Append("• ").Append(t.Name)
              .Append(" (").Append(t.Belt?.Name ?? "—")
              .Append(" · ").Append(t.Category?.Name ?? "—")
              .Append(')');
        }

        sb.AppendLine().AppendLine().Append(_localizador[NotaSeguridadClave]);
        return sb.ToString().Trim();
    }

    private string ExplicarConcepto(Concepto concepto, IReadOnlyList<Technique> tecnicas)
    {
        var sb = new StringBuilder(concepto.Definicion);

        // Conceptos con técnicas RELACIONABLES en el catálogo (relación comprobable).
        var relacionadas = concepto.ClaveDeRelacion is null
            ? Array.Empty<Technique>()
            : tecnicas.Where(t => TextNormalizer.Normalize(t.Position?.Name ?? string.Empty)
                    .Contains(concepto.ClaveDeRelacion)
                || TextNormalizer.Normalize(t.Name).Contains(concepto.ClaveDeRelacion))
            .Take(3).ToArray();

        if (relacionadas.Length > 0)
        {
            sb.AppendLine().AppendLine()
              .Append("Técnicas del catálogo relacionadas: ")
              .Append(string.Join(", ", relacionadas.Select(t => t.Name))).Append('.');
        }

        sb.AppendLine().AppendLine().Append(_localizador[NotaSeguridadClave]);
        return sb.ToString().Trim();
    }

    private string RespuestaAprendizaje(IReadOnlyList<Technique> tecnicas, string? nivel)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Para progresar en tu aprendizaje:");
        sb.AppendLine("• Practicá de forma consistente: mejor 3 sesiones cortas por semana que una larga.");
        sb.AppendLine("• Repasá en intervalos: volvé a una técnica unos días después de aprenderla.");
        sb.AppendLine("• Filmá o anotá los detalles que te costaron y llevá tus dudas a la clase.");
        sb.AppendLine("• Aprendé una técnica por vez y entendé su objetivo antes de repetir el paso.");
        sb.AppendLine("• Preguntale a tu instructor antes de probar una variación nueva.");

        var sugeridas = SugerenciasPara(tecnicas, nivel);
        if (sugeridas.Count > 0)
        {
            sb.AppendLine().Append("Para empezar, podés estudiar: ")
              .Append(string.Join(" · ", sugeridas)).Append('.');
        }

        sb.AppendLine().AppendLine().Append(_localizador[NotaSeguridadClave]);
        return sb.ToString().Trim();
    }

    /// <summary>Preguntas de seguimiento: técnicas del nivel del usuario (si se conoce).</summary>
    private static IReadOnlyList<string> SugerenciasPara(IReadOnlyList<Technique> tecnicas, string? nivel)
    {
        var base_ = string.IsNullOrWhiteSpace(nivel)
            ? tecnicas
            : tecnicas.Where(t => TextNormalizer.AreEqual(t.Belt?.Name, nivel)).ToList();

        if (base_.Count == 0)
            base_ = tecnicas;

        return base_
            .OrderBy(t => t.Name, StringComparer.CurrentCulture)
            .Take(3)
            .Select(t => $"¿Cómo se hace {t.Name}?")
            .ToList();
    }

    /// <summary>Sugerencias iniciales de la pantalla (definidas por producto).</summary>
    /// <summary>Preguntas sugeridas traducidas al idioma de la petición.</summary>
    private IReadOnlyList<string> SugerenciasFijas =>
        SugerenciasFijasClaves.Select(k => (string)_localizador[k]).ToList();

    public static IReadOnlyList<string> SugerenciasIniciales(IStringLocalizer<UiResources> localizador)
    {
        ArgumentNullException.ThrowIfNull(localizador);
        return SugerenciasFijasClaves.Select(k => (string)localizador[k]).ToList();
    }

    /// <summary>
    /// Técnicas comprobablemente relacionadas: misma categoría o misma
    /// posición (excluye la propia; prioriza el mismo cinturón).
    /// </summary>
    private static IReadOnlyList<string> Relacionadas(Technique t, IReadOnlyList<Technique> todas) =>
        todas.Where(o => o.Id != t.Id
                && (o.CategoryId == t.CategoryId || o.PositionId == t.PositionId))
            .OrderByDescending(o => o.BeltId == t.BeltId)
            .ThenBy(o => o.Name, StringComparer.CurrentCulture)
            .Take(3)
            .Select(o => o.Name)
            .ToList();

    private static bool PideExplicacion(string normalizado) =>
        Contiene(normalizado, "EXPLIC", "COMO SE HACE", "COMO HAGO", "ENSEN", "APRENDO", "TECNICA", "MOVIMIENTO");

    private static bool Contiene(string normalizado, params string[] claves) =>
        claves.Any(normalizado.Contains);

    private static List<string> ParsearLista(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            var lista = JsonSerializer.Deserialize<List<string>>(json);
            return lista?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();
        }
        catch (JsonException)
        {
            // Texto plano (filas antiguas): se usa tal cual.
            return new List<string> { json };
        }
    }

    private static string Recortar(string texto, int max)
    {
        var limpio = texto.Trim();
        return limpio.Length <= max ? limpio : limpio[..max] + "…";
    }

    // ── Prompt de IA ──────────────────────────────────────────────────────

    private GuiaPeticionIa ConstruirPeticion(string texto, IReadOnlyList<Technique> tecnicas, string? nivel)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Sos \"Guía del Practicante\" de Mi Camino BJJ: un acompañante educativo de Brazilian Jiu-Jitsu.");
        sb.AppendLine("Reglas obligatorias:");
        sb.AppendLine("1. Solo podés afirmar que una técnica existe si figura en la biblioteca de abajo. Si no está, decí que no está registrada en el sistema; nunca la presentes como registrada.");
        sb.AppendLine("2. Nunca inventes progreso, entrenamientos ni datos personales del usuario; el único dato que tenés es su cinturón.");
        sb.AppendLine("3. Nunca reveles contraseñas, hashes, correos electrónicos, tokens, datos de otros usuarios ni información de auditoría.");
        sb.AppendLine("4. Explicaciones de movimientos: educativas, recomendando práctica supervisada por un instructor; jamás instrucciones para lesionar a otra persona.");
        sb.AppendLine("5. Respondé en español, claro y breve (máximo 150 palabras), sin HTML ni tablas.");
        sb.AppendLine("6. Si la pregunta no está relacionada con BJJ o el aprendizaje, respondé brevemente y orientá al entrenamiento.");
        sb.AppendLine();
        sb.AppendLine(nivel is null
            ? "Nivel del usuario: desconocido (no asumas uno ni pidas datos privados)."
            : $"Nivel del usuario (cinturón): {nivel}. Adaptá la explicación a ese nivel.");
        sb.AppendLine();
        sb.AppendLine("Biblioteca registrada (única fuente):");

        foreach (var t in tecnicas)
        {
            sb.Append("- ").Append(t.Name);
            var alias = t.Aliases?.Select(a => a.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            if (alias is { Count: > 0 })
                sb.Append(" | alias: ").Append(string.Join(", ", alias));
            sb.Append(" | cinturón ").Append(t.Belt?.Name ?? "—")
              .Append(" | categoría ").Append(t.Category?.Name ?? "—")
              .Append(" | posición ").Append(t.Position?.Name ?? "—");
            if (!string.IsNullOrWhiteSpace(t.Objective))
                sb.Append(" | objetivo: ").Append(Recortar(t.Objective, 160));
            sb.AppendLine();
        }

        return new GuiaPeticionIa(sb.ToString().Trim(), texto);
    }

    // ── Conceptos (conocimiento general de BJJ, no datos de la base) ──────

    private sealed record Concepto(string Definicion, string[] Claves, string? ClaveDeRelacion);

    private static readonly Concepto[] Conceptos =
    [
        new(
            "La guardia es la posición en la que estás en el suelo frente a tu rival y te defendés con las piernas: "
            + "te permite controlarlo, crear distancias y buscar sumisiones. Existen muchas variantes (cerrada, abierta, "
            + "de mariposa, de tipo) y dominarlas es parte central del BJJ.",
            ["GUARDIA"], "GUARDIA"),
        new(
            "La montada es una posición dominante: quedás encima del rival con control de su torso. Desde ahí se buscan "
            + "sumisiones o transiciones; defenderla y recuperar la guardia es una de las primeras habilidades a aprender.",
            ["MONTADA"], "MONTADA"),
        new(
            "Controlar la espalda del rival es una de las posiciones más ventajosas: se busca con gancho de piernas y "
            + "control de cintura para amenazar estrangulaciones. Aprender a escapar de la espalda es prioritario al inicio.",
            ["ESPALDA"], "ESPALDA"),
        new(
            "El paso de guardia consiste en cruzar las piernas del rival para llegar a posiciones dominantes como la "
            + "montada o el control lateral. Requiere presión, ángulos y paciencia, no fuerza bruta.",
            ["PASO DE GUARDIA", "PASAR LA GUARDIA", "GUARD PASSING"], "GUARDIA"),
        new(
            "Una sumisión termina el combate mediante una luxación articular o una estrangulación. Se aplica DE FORMA "
            + "CONTROLADA: se da tiempo al compañero para defender o rendirse y jamás se fuerza (evita lesiones).",
            ["SUMISION", "SUMMISION"], null),
        new(
            "Los cinturones marcan el progreso técnico: en nuestro catálogo encontrás técnicas de cinturón Blanco, Azul, "
            + "Violeta y Marrón. El progreso depende de la constancia y de la evaluación de tu instructor, no de pruebas "
            + "automáticas.",
            ["CINTURON", "BELT"], null)
    ];
}
