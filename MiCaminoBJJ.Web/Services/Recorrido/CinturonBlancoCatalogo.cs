using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;

namespace MiCaminoBJJ.Web.Services.Recorrido;

/// <summary>
/// Catálogo de contenido educativo del CINTURÓN BLANCO (Etapa 6A).
///
/// Decisiones de arquitectura:
/// - Contenido en CÓDIGO, no en la base: esta etapa no requiere migración SQL
///   Server y por lo tanto no toca el esquema existente (ver informe final
///   para el diseño propuesto de una tabla de asociación técnica↔recorrido).
/// - NO duplica entidades: las técnicas de la biblioteca se referencian por
///   NOMBRE (<see cref="FichaBlanco.TecnicaRelacionada"/>), se resuelven en
///   runtime contra <c>ITecnicasConsultaService</c> y se enlazan a su ficha
///   real conservando su cinturón principal.
/// - Referencia pedagógica declarada por la usuaria: Jiu-Jitsu University
///   (Saulo Ribeiro y Kevin Howell). Los textos son originales; NO se copió
///   ningún párrafo del libro.
/// - Videos: SOLO los cuatro enlaces confirmados por la usuaria. Los módulos
///   sin demostración muestran teoría y el aviso discreto de "video pendiente".
///
/// Validaciones de falla temprana (estático): slugs únicos, 3 a 6 pasos por
/// ficha, módulos existentes y videos con URL de YouTube válida. Si algo del
/// catálogo está mal, la app no arranca y las pruebas lo detectan.
/// </summary>
public static class CinturonBlancoCatalogo
{
    public const string Titulo = "Cinturón Blanco";
    public const string Subtitulo = "Supervivencia y defensa básica";
    public const string Descripcion =
        "Todo camino comienza con una base sólida. Aprendé a protegerte, mantener la calma bajo presión "
        + "y reconocer las posiciones fundamentales del Brazilian Jiu-Jitsu. Antes de atacar, aprendé a sobrevivir.";

    public const string Introduccion =
        "El cinturón blanco se entrena con paciencia: una buena postura, el uso eficiente de la energía "
        + "y la práctica supervisada por un instructor rinden más que la fuerza bruta. Repetí los movimientos "
        + "despacio, preguntá lo que no entiendas y cuidá a tu compañero de entrenamiento. La velocidad llega "
        + "con la técnica, nunca al revés.";

    /// <summary>Marco pedagógico declarado (verificado por la usuaria como referencia del programa).</summary>
    public const string MarcoPedagogico =
        "Marco pedagógico del recorrido: Jiu-Jitsu University, de Saulo Ribeiro y Kevin Howell "
        + "(progresión supervivencia → escapes → guardias → pases → sumisiones). Los textos de este recorrido "
        + "son originales y no reproducen párrafos del libro.";

    public const string NotaVideoPendiente =
        "Video pendiente: todavía no hay demostración confirmada para este módulo.";

    /// <summary>Texto de estado que acompaña a TODAS las fichas (nada está validado por un profesional).</summary>
    public const string EstadoRevisionFicha =
        "Pendiente de revisión del instructor: el contenido fue redactado para este recorrido y todavía "
        + "no fue validado por un profesional. No lo presentes como material verificado.";

    public const string ReferenciaSinVerificar =
        "Sin referencia bibliográfica verificada para esta ficha; pendiente de revisión del instructor.";

    public const string NotaVideoFicha =
        "Correspondencia visual pendiente de validación técnica.";

    /// <summary>Aviso de material complementario (módulo 5).</summary>
    public const string NotaMaterialComplementario =
        "Material complementario: estas fichas pueden corresponder a técnicas cuyo cinturón principal es "
        + "azul o morado. Se muestran dentro del recorrido blanco sin cambiarles su cinturón principal.";

    public const string NotaSupervivenciaVsEscape =
        "SUPERVIVENCIA = protegerse y estabilizar la defensa. ESCAPE = ejecutar una transición para salir "
        + "de una posición desfavorable. No son lo mismo: primero sobrevivís, después salís.";

    private static readonly ExternalMediaParser Parser = new();

    private static readonly IReadOnlyList<ModuloBlanco> ModulosInternos = Construir();

    /// <summary>Los cinco módulos, en orden del recorrido.</summary>
    public static IReadOnlyList<ModuloBlanco> Modulos => ModulosInternos;

    /// <summary>Todas las fichas del recorrido, en orden de módulo.</summary>
    public static IReadOnlyList<FichaBlanco> Fichas =>
        ModulosInternos.SelectMany(m => m.Fichas).ToList();

    public static ModuloBlanco? ModuloPorSlug(string? slug) =>
        string.IsNullOrWhiteSpace(slug)
            ? null
            : ModulosInternos.FirstOrDefault(m => string.Equals(m.Slug, slug, StringComparison.OrdinalIgnoreCase));

    public static FichaBlanco? FichaPorSlug(string? slug) =>
        string.IsNullOrWhiteSpace(slug)
            ? null
            : Fichas.FirstOrDefault(f => string.Equals(f.Slug, slug, StringComparison.OrdinalIgnoreCase));

    public static ModuloBlanco? ModuloAnterior(ModuloBlanco modulo) =>
        ModulosInternos.FirstOrDefault(m => m.Id == modulo.Id - 1);

    public static ModuloBlanco? ModuloSiguiente(ModuloBlanco modulo) =>
        ModulosInternos.FirstOrDefault(m => m.Id == modulo.Id + 1);

    public static FichaBlanco? FichaAnterior(FichaBlanco ficha) =>
        AnteriorSiguiente(Fichas, f => f.Slug == ficha.Slug, haciaAdelante: false);

    public static FichaBlanco? FichaSiguiente(FichaBlanco ficha) =>
        AnteriorSiguiente(Fichas, f => f.Slug == ficha.Slug, haciaAdelante: true);

    private static FichaBlanco? AnteriorSiguiente(
        IReadOnlyList<FichaBlanco> lista, Func<FichaBlanco, bool> selector, bool haciaAdelante)
    {
        var indice = -1;
        for (var i = 0; i < lista.Count; i++)
        {
            if (selector(lista[i]))
            {
                indice = i;
                break;
            }
        }

        if (indice < 0)
            return null;

        var destino = haciaAdelante ? indice + 1 : indice - 1;
        return destino >= 0 && destino < lista.Count ? lista[destino] : null;
    }

    /// <summary>
    /// Valida el catálogo completo: falla temprana si hay slugs repetidos,
    /// fichas fuera del rango de 3 a 6 pasos, módulos inexistentes o videos
    /// inválidos. Se ejecuta una sola vez al primer acceso al tipo.
    /// </summary>
    private static IReadOnlyList<ModuloBlanco> Construir()
    {
        var modulos = new[]
        {
            ModuloPrimerosPasos(),
            ModuloFundamentosDefensivos(),
            ModuloSupervivenciaEnMontada(),
            ModuloSupervivenciaEnControlLateral(),
            ModuloEscapesBasicos()
        };

        var slugsModulo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var slugsFicha = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Se recolectan TODOS los slugs de ficha ANTES de validar las
        // referencias cruzadas: durante la inicialización estática todavía no
        // existe la lista global (se asigna recién al terminar este método).
        foreach (var modulo in modulos)
            foreach (var ficha in modulo.Fichas)
            {
                if (!slugsFicha.Add(ficha.Slug))
                    throw new InvalidOperationException($"Slug de ficha duplicado: '{ficha.Slug}'.");
            }

        foreach (var modulo in modulos)
        {
            if (!slugsModulo.Add(modulo.Slug))
                throw new InvalidOperationException($"Slug de módulo duplicado: '{modulo.Slug}'.");

            foreach (var ficha in modulo.Fichas)
            {
                if (ficha.ModuloId != modulo.Id)
                {
                    throw new InvalidOperationException(
                        $"La ficha '{ficha.Slug}' declara módulo {ficha.ModuloId} pero pertenece al módulo {modulo.Id}.");
                }

                if (ficha.Pasos.Count is < 3 or > 6)
                {
                    throw new InvalidOperationException(
                        $"La ficha '{ficha.Slug}' tiene {ficha.Pasos.Count} pasos: deben ser entre 3 y 6.");
                }
            }

            foreach (var relacionada in modulo.FichasRelacionadas)
            {
                if (!slugsFicha.Contains(relacionada))
                {
                    throw new InvalidOperationException(
                        $"El módulo '{modulo.Slug}' referencia la ficha inexistente '{relacionada}'.");
                }
            }
        }

        return modulos;
    }

    private static FichaBlanco Ficha(
        string slug,
        string nombre,
        int moduloId,
        string tipoContenido,
        string posicionInicial,
        string objetivo,
        string introduccion,
        string[] pasos,
        string[] principios,
        string[] errores,
        string[] precauciones,
        string? videoUrl,
        string? tecnicaRelacionada,
        string? cinturonPrincipal,
        string? referenciaBibliografica,
        string estadoRevision) => new(
            Slug: slug,
            Nombre: nombre,
            ModuloId: moduloId,
            TipoContenido: tipoContenido,
            PosicionInicial: posicionInicial,
            Objetivo: objetivo,
            Introduccion: introduccion,
            Pasos: pasos,
            Principios: principios,
            ErroresFrecuentes: errores,
            Precauciones: precauciones,
            Video: videoUrl is null
                ? null
                : VideoRecorrido.Crear(videoUrl, nombre, NotaVideoFicha, Parser),
            TecnicaRelacionada: tecnicaRelacionada,
            CinturonPrincipal: cinturonPrincipal,
            ReferenciaBibliografica: referenciaBibliografica,
            EstadoRevision: estadoRevision,
            RequiereRevisionInstructor: true);

    // ── MÓDULO 1 ──────────────────────────────────────────────────────────

    private static ModuloBlanco ModuloPrimerosPasos() => new(
        Id: 1,
        Slug: "primeros-pasos",
        Titulo: "Primeros pasos",
        Resumen: "Qué es el Jiu-Jitsu, qué se espera de vos como principiante y cómo se entrena con seguridad en el tatami.",
        Puntos: new[]
        {
            new PuntoContenido(
                "Qué es el BJJ",
                "El Brazilian Jiu-Jitsu es un arte marcial de suelo: se busca controlar a un oponente con "
                + "palancas, técnica y respiración, sin golpes. Como se apoya en la posición y no en la "
                + "fuerza bruta, una persona más liviana puede defenderse de una más grande si aplica bien "
                + "los principios."),
            new PuntoContenido(
                "Objetivo del cinturón blanco",
                "El cinturón blanco es la etapa de entrada: reconocer el peligro, mantener la calma, "
                + "proteger cuello y extremidades y familiarizarte con las posiciones básicas. No se trata "
                + "de ganar combates, sino de entender dónde estás parado y cómo volver a estar cómodo."),
            new PuntoContenido(
                "Posiciones básicas",
                "Vas a encontrar la guardia (boca arriba, con las piernas enfrente), la montada (un oponente "
                + "encima, a caballo sobre tu torso), el control lateral (desde el costado) y la media guardia "
                + "(cuando atrapás una pierna). Primero aprendé a identificar en cuál estás: cada una tiene "
                + "su propia lógica de defensa."),
            new PuntoContenido(
                "Etiqueta y seguridad en el tatami",
                "Saludá al entrar y al entrenar con cada compañero, mantené el kimono limpio y las uñas "
                + "cortas, y avisá si tenés alguna lesión. Nunca practiques una técnica nueva a máxima "
                + "velocidad sin que el instructor la haya revisado antes."),
            new PuntoContenido(
                "Qué significa rendirse o hacer tapping",
                "Tocar al compañero (o el tatami) con la mano dos veces, o decir \"stop\", significa que "
                + "frenás al instante. Rendirse no es fracasar: es cuidar tu cuerpo. Y también es tu "
                + "responsabilidad soltar una posición en el momento exacto en que tu compañero rinde."),
            new PuntoContenido(
                "Cómo estudiar las técnicas",
                "Mirá la técnica completa una vez, practicá un solo paso a la vez y preguntá lo que no "
                + "entiendas. Anotar los pasos principales después de clase ayuda a fijar la secuencia: "
                + "la repetición lenta construye la memoria que después se vuelve velocidad.")
        },
        EstadoVideo: EstadoVideoModulo.Pendiente,
        Fichas: Array.Empty<FichaBlanco>(),
        FichasRelacionadas: Array.Empty<string>());

    // ── MÓDULO 2 ──────────────────────────────────────────────────────────

    private static ModuloBlanco ModuloFundamentosDefensivos() => new(
        Id: 2,
        Slug: "fundamentos-defensivos",
        Titulo: "Fundamentos defensivos",
        Resumen: "Las seis herramientas que usás en todas las posiciones: cuello, codos, estructuras, cadera, distancia y respiración.",
        Puntos: new[]
        {
            new PuntoContenido(
                "Protección del cuello",
                "Mantené el mentón bajo y las manos cerca de la cara cuando el oponente presiona. Un cuello "
                + "expuesto es la puerta de entrada a las estrangulaciones: cerrala antes de que empiecen, "
                + "no después."),
            new PuntoContenido(
                "Posición de los codos",
                "Los codos pegados al torso protegen las costillas y evitan que el oponente meta los brazos "
                + "debajo de los tuyos. Un codo abierto deja un hueco, y ese hueco se convierte rápido en "
                + "control."),
            new PuntoContenido(
                "Uso de estructuras defensivas (frames)",
                "Una estructura es apoyar el esqueleto —antebrazo, rodilla, cadera— contra el oponente para "
                + "sostener espacio sin gastar fuerza. Cuando el peso cae sobre un frame sólido, la presión "
                + "se reparte en los huesos y no en los músculos."),
            new PuntoContenido(
                "Movimiento de cadera",
                "La cadera es tu motor: puentear, barrer y recuperar guardia nacen de mover la pelvis con "
                + "buen timing. Antes de empujar con los brazos, aprendé a desplazar la cadera y a sentir "
                + "cuándo se desequilibra el otro."),
            new PuntoContenido(
                "Manejo de la distancia",
                "La distancia corta favorece a quien está arriba y controlando; la distancia media te deja "
                + "escapar. Observá si el oponente está sobre vos, a media distancia o de pie, y elegí la "
                + "respuesta que corresponde a esa situación."),
            new PuntoContenido(
                "Control de la respiración y la calma",
                "Respirar parejo baja la tensión y te deja pensar. El pánico acelera el consumo de energía "
                + "y corta el razonamiento: si sentís que te desbordás, frená, respirá y volvé a construir "
                + "tu estructura defensiva.")
        },
        EstadoVideo: EstadoVideoModulo.Pendiente,
        Fichas: Array.Empty<FichaBlanco>(),
        FichasRelacionadas: Array.Empty<string>());

    // ── MÓDULO 3 ──────────────────────────────────────────────────────────

    private static ModuloBlanco ModuloSupervivenciaEnMontada() => new(
        Id: 3,
        Slug: "supervivencia-en-montada",
        Titulo: "Supervivencia en montada",
        Resumen: "Qué hacer cuando un oponente está encima a caballo: primero protegerte, después pensar en salir.",
        Puntos: new[]
        {
            new PuntoContenido(
                "Reconocer la posición de montada",
                "Estás boca arriba con el oponente a caballo sobre tu torso, con las rodillas en el suelo a "
                + "cada costado. Aceptar dónde estás es el primer paso: negarlo te hace gastar energía en "
                + "movimientos que no llevan a ningún lado."),
            new PuntoContenido(
                "Protegerse antes de escapar",
                "Antes de intentar cualquier salida, ordená tu defensa: codos pegados al cuerpo, manos "
                + "cerca del cuello y mentón bajo. Un escape apresurado con la defensa abierta casi siempre "
                + "termina en una sumisión peor."),
            new PuntoContenido(
                "Evitar extender los brazos",
                "Los brazos estirados hacia arriba son palancas listas para ser agarrados. Mantené los codos "
                + "cerca del torso y no empujes al oponente si eso te despega del suelo y te deja sin base."),
            new PuntoContenido(
                "Mantener estructuras defensivas",
                "Cuando el oponente sube el peso, sostenelo con estructuras de antebrazo y rodilla en lugar "
                + "de tensar todo el cuerpo. Las estructuras compran el tiempo que necesitás para pensar con "
                + "claridad."),
            new PuntoContenido(
                "Identificar oportunidades seguras de movimiento",
                "Esperá un momento estable —el oponente se recarga, cambia de mano o pierde el equilibrio— "
                + "para moverte. Moverse sin una señal suele gastar energía sin resultado y volver a abrir "
                + "tu defensa.")
        },
        EstadoVideo: EstadoVideoModulo.Pendiente,
        Fichas: Array.Empty<FichaBlanco>(),
        FichasRelacionadas: new[] { "escape-de-montada-con-codos" });

    // ── MÓDULO 4 ──────────────────────────────────────────────────────────

    private static ModuloBlanco ModuloSupervivenciaEnControlLateral() => new(
        Id: 4,
        Slug: "supervivencia-en-control-lateral",
        Titulo: "Supervivencia en control lateral",
        Resumen: "Cómo sobrevivir cuando te tienen apretado de costado y recuperar el espacio perdido.",
        Puntos: new[]
        {
            new PuntoContenido(
                "Reconocer el control lateral",
                "El oponente está de costado sobre tu torso, con el pecho contra el tuyo y los brazos o las "
                + "rodillas controlando tu cadera. Su objetivo es pegarte al suelo; el tuyo es no quedar "
                + "completamente plano."),
            new PuntoContenido(
                "Proteger cuello y brazos",
                "Una mano cerca del cuello y los codos cerrados evitan que cuelen la solapa o aíslen un "
                + "brazo. Lo primero que tenés que defender es el cuello: todo lo demás se recupera después."),
            new PuntoContenido(
                "Evitar quedar completamente plano",
                "Si quedás de espaldas contra el tatami con los dos hombros pegados, perdés movilidad. "
                + "Buscá quedarte de costado, con una rodilla adentro, aunque sea apenas un poco de espacio."),
            new PuntoContenido(
                "Utilizar estructuras defensivas",
                "Colocá el antebrazo en la cadera o el cuello del oponente para sostener distancia. La "
                + "estructura no empuja con fuerza: evita que el espacio se cierre mientras preparás el "
                + "siguiente movimiento."),
            new PuntoContenido(
                "Comprender la importancia de recuperar espacio",
                "Todo avance contra el control lateral empieza con un centímetro de aire: primero separás, "
                + "después metés la rodilla y recién después volvés a la guardia. Recuperar espacio es la "
                + "mitad del trabajo.")
        },
        EstadoVideo: EstadoVideoModulo.Pendiente,
        Fichas: Array.Empty<FichaBlanco>(),
        FichasRelacionadas: new[] { "escape-de-control-lateral-recuperando-guardia" });

    // ── MÓDULO 5 ──────────────────────────────────────────────────────────

    private static ModuloBlanco ModuloEscapesBasicos() => new(
        Id: 5,
        Slug: "escapes-basicos",
        Titulo: "Escapes básicos y recuperaciones",
        Resumen: "Cuando la defensa está ordenada, llega el momento de moverse: escapes de montada y control lateral, "
                  + "recuperación de guardia y reconocimiento del armbar.",
        Puntos: new[]
        {
            new PuntoContenido(
                "Supervivencia primero, escape después",
                NotaSupervivenciaVsEscape),
            new PuntoContenido(
                "Material complementario",
                NotaMaterialComplementario),
            new PuntoContenido(
                "Practicá con supervisión",
                "Cada ficha de este módulo se practica con un compañero y bajo la mirada del instructor. "
                + "Mirá el video, repasá los pasos y recién después probalo a media velocidad.")
        },
        EstadoVideo: EstadoVideoModulo.ConVideo,
        Fichas: new[]
        {
            FichaEscapeDeMontadaConCodos(),
            FichaEscapeDeControlLateral(),
            FichaRecuperacionDeGuardiaDesdeMediaGuardia(),
            FichaDefensaAnteArmbar()
        },
        FichasRelacionadas: Array.Empty<string>());

    // ── Fichas del módulo 5 ───────────────────────────────────────────────

    private static FichaBlanco FichaEscapeDeMontadaConCodos() => Ficha(
        slug: "escape-de-montada-con-codos",
        nombre: "Escape de montada con codos",
        moduloId: 5,
        tipoContenido: "Técnica de escape",
        posicionInicial: "Montada: el oponente a caballo sobre tu torso, vos boca arriba con la espalda en el suelo.",
        objetivo: "Proteger cuello y brazos, desequilibrar al oponente con el puente y salir hacia una posición más segura.",
        introduccion:
            "Es el primer escape que se practica en el cinturón blanco porque aparece en casi todas las rondas. "
            + "Trabaja con los codos como base: primero te cubrís, después movés la cadera y recién al final salís. "
            + "La técnica completa con su ficha en la biblioteca es \"Escape de montada\".",
        pasos: new[]
        {
            "Colocá los codos pegados al cuerpo y protegé el cuello.",
            "Realizá un puente explosivo hacia un lado para desequilibrar al oponente.",
            "Cuando el oponente reaccione, usá la rodilla para hacer palanca.",
            "Escapá a media guardia o guardia cerrada."
        },
        principios: new[]
        {
            "La defensa va antes que el movimiento: sin codos y cuello cubiertos, no se sale.",
            "El puente es corto y explosivo, no una lucha de fuerza contra el peso del otro.",
            "Escapar es una secuencia de pasos, no un solo esfuerzo desesperado."
        },
        errores: new[]
        {
            "Estirar los brazos hacia arriba y regalar una palanca.",
            "Intentar zafarse de una sola vez sin haber desequilibrado antes al oponente.",
            "Contener la respiración durante el esfuerzo y llegar sin energía al final."
        },
        precauciones: new[]
        {
            "Practicá el puente con control: no levantes en el aire a tu compañero.",
            "Frená y avisá al instructor si sentís dolor en el cuello o la espalda.",
            "Hacé tapping si quedás atrapado en lugar de forzar la salida."
        },
        videoUrl: "https://youtube.com/shorts/ynSK1oYc_gg",
        tecnicaRelacionada: "Escape de montada",
        cinturonPrincipal: "Blanco",
        referenciaBibliografica: null,
        estadoRevision:
            "Pendiente de revisión del instructor: los pasos provienen del catálogo interno de la biblioteca "
            + "y la correspondencia con el video aún no fue validada técnicamente.");

    private static FichaBlanco FichaEscapeDeControlLateral() => Ficha(
        slug: "escape-de-control-lateral-recuperando-guardia",
        nombre: "Escape de control lateral recuperando guardia",
        moduloId: 5,
        tipoContenido: "Técnica de escape",
        posicionInicial: "Control lateral: el oponente de costado sobre tu torso, con el pecho pegado al tuyo.",
        objetivo: "Recuperar la guardia —llevando las piernas entre ambos— para salir del control lateral.",
        introduccion:
            "Cuando el oponente ya está estable de costado, el trabajo es recuperar espacio y meter la pierna. "
            + "Este escape combina la defensa del módulo anterior con el movimiento de cadera para volver a una "
            + "posición donde puedas defenderte con las piernas por delante.",
        pasos: new[]
        {
            "Protegé el cuello con una mano y mantené los codos cerca del cuerpo.",
            "Creá un poco de espacio con la cadera y el antebrazo sobre la cadera del oponente.",
            "Sacá la rodilla de adentro hacia la cintura del oponente para ganar la media guardia.",
            "Seguí moviendo la cadera hasta recuperar la guardia cerrada con las piernas por delante."
        },
        principios: new[]
        {
            "Sin espacio no hay escape: primero separás, después avanzás.",
            "El cuello y los codos se protegen durante todo el movimiento, también al final.",
            "Recuperar guardia es una transición ordenada, no un sprint."
        },
        errores: new[]
        {
            "Empujar con los brazos en lugar de mover la cadera.",
            "Dejar el cuello sin cubrir mientras se intenta la salida.",
            "Quedar plano en la espalda otra vez y perder el ángulo ganado."
        },
        precauciones: new[]
        {
            "Pedí a tu compañero que te avise si el peso se vuelve insoportable.",
            "No fuerces la rodilla adentro si sentís dolor en la cadera.",
            "Frená el movimiento si la cabeza del oponente puede chocar contra tu cara."
        },
        videoUrl: "https://youtu.be/eDUjKxV6wb0",
        tecnicaRelacionada: "Puente y giro desde control lateral",
        cinturonPrincipal: "Violeta",
        referenciaBibliografica: null,
        estadoRevision:
            "Pendiente de revisión del instructor: los pasos son preliminares (la técnica relacionada del "
            + "catálogo no tiene pasos cargados) y la correspondencia con el video aún no fue validada.");

    private static FichaBlanco FichaRecuperacionDeGuardiaDesdeMediaGuardia() => Ficha(
        slug: "recuperacion-de-guardia-desde-media-guardia",
        nombre: "Recuperación de guardia desde media guardia",
        moduloId: 5,
        tipoContenido: "Recuperación de posición",
        posicionInicial: "Media guardia: el oponente controlando una de tus piernas mientras vos quedás de costado.",
        objetivo: "Sacar las piernas por delante del oponente y cerrar la guardia para recuperar la posición defensiva completa.",
        introduccion:
            "La media guardia es una posición intermedia: ni estás controlado del todo ni estás libre. "
            + "Recuperar la guardia desde ahí te devuelve el mapa completo de defensa, con las piernas enfrente "
            + "y más opciones para moverte.",
        pasos: new[]
        {
            "Quedate de costado con los codos cerrados y el cuello protegido.",
            "Llevá la pierna de adentro por debajo de la pierna del oponente.",
            "Usá la cadera para separar y meter la otra pierna entre los dos cuerpos.",
            "Cerrá la guardia con los tobillos y volvé las manos a posición defensiva."
        },
        principios: new[]
        {
            "Trabajá de costado: nunca resuelvas de espaldas planas en el suelo.",
            "La cadera separa y avanza; los brazos solo protegen.",
            "El movimiento termina recién cuando la guardia está cerrada."
        },
        errores: new[]
        {
            "Quedarse de espaldas empujando sin meter la rodilla.",
            "Olvidar el cuello mientras se libera la pierna.",
            "Apurar el cierre de guardia y terminar sin ángulo ni control."
        },
        precauciones: new[]
        {
            "Si el oponente cae con todo el peso sobre tu rodilla, frená el movimiento al instante.",
            "Practicá primero sin oposición hasta que la secuencia sea clara.",
            "Hacé tapping si la pierna queda trabada y el compañero no suelta."
        },
        videoUrl: "https://youtu.be/sCmv4Jc0qlU",
        tecnicaRelacionada: null,
        cinturonPrincipal: null,
        referenciaBibliografica: null,
        estadoRevision:
            "Pendiente de revisión del instructor: no hay técnica equivalente en la biblioteca, los pasos "
            + "son preliminares y la correspondencia con el video aún no fue validada. Sin cinturón principal "
            + "asignado a propósito.");

    private static FichaBlanco FichaDefensaAnteArmbar() => Ficha(
        slug: "defensa-ante-armbar",
        nombre: "Defensa ante armbar",
        moduloId: 5,
        tipoContenido: "Reconocimiento y seguridad",
        posicionInicial: "Guardia cerrada del oponente: vos con un brazo atrapado mientras él gira las caderas hacia tu costado.",
        objetivo: "Reconocer la palanca a tiempo, proteger el brazo hasta poder salir y saber cuándo rendirse a salvo.",
        introduccion:
            "El armbar es una de las sumisiones más comunes del BJJ. En este recorrido no se enseña a aplicarla: "
            + "se enseña a reconocerla, a evitar que se cierre y a rendirte a tiempo. Saber cuándo rendirse "
            + "es parte de la técnica.",
        pasos: new[]
        {
            "Reconocé la señal de peligro: el oponente controla tu brazo, rompe tu postura y sube una pierna sobre tu cara.",
            "No estires el brazo amenazado: mantené el codo cerca de tu cuerpo.",
            "Trabajá la postura: sentí el peso hacia adelante y buscá apoyar las manos para recuperar el eje.",
            "Si la palanca ya está cerrada, rendí a tiempo con tapping antes de que la articulación se lesione."
        },
        principios: new[]
        {
            "El brazo nunca se estira contra una palanca que ya está cerrada.",
            "Mejor perder la posición que perder el codo.",
            "El tapping es una herramienta de entrenamiento, no un fracaso."
        },
        errores: new[]
        {
            "Estirar el brazo para \"zafarse\" y terminar con la palanca más corta.",
            "Ignorar la pierna que cruza la cara hasta que ya no hay salida.",
            "Enojarse y forzar la articulación en lugar de rendirse."
        },
        precauciones: new[]
        {
            "Hacé tapping ante el primer tirón en el codo; no esperes a que duela.",
            "Tu compañero debe soltar al instante: es regla ineludible del tatami.",
            "Practicá la defensa sin cerrar la palanca a velocidad completa."
        },
        videoUrl: "https://youtu.be/yLGmZxSf2Vo",
        tecnicaRelacionada: "Armbar desde guardia cerrada",
        cinturonPrincipal: "Blanco",
        referenciaBibliografica: null,
        estadoRevision:
            "Pendiente de revisión del instructor: la ficha es de RECONOCIMIENTO Y SEGURIDAD (no enseña a "
            + "aplicar la palanca), los pasos son preliminares y la correspondencia con el video aún no fue "
            + "validada técnicamente.");
}
