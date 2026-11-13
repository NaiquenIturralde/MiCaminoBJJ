/* =====================================================================================
   MI CAMINO BJJ — IMPORTACIÓN DEFINITIVA DE LAS 18 TÉCNICAS DE TecnicasYLinks.pdf
   -------------------------------------------------------------------------------------
   Motor    : SQL Server 2019 (15.0.4382.1) — (localdb)\MSSQLLocalDB
   Base     : MiCaminoBJJ_Web_TFI_2026   (única base autorizada)
   Alcance  : SOLO datos. No crea ni elimina tablas, no ejecuta migraciones,
              no toca usuarios, contraseñas ni roles.
   Estado   : EJECUTADO el 2026-11-12 sobre MiCaminoBJJ_Web_TFI_2026
              (simulación previa con ROLLBACK verificada + COMMIT único).
              El parámetro @ModoSimulacion vuelve a 1 por defecto: una
              re-ejecución accidental SIMULA y no escribe nada.

   GARANTÍAS
   ---------
   1. Idempotente: NOT EXISTS / comparación de estado en cada paso. Una segunda
      ejecución no crea filas nuevas ni duplica auditoría.
   2. Sin IDs supuestos: cinturones, categorías, posiciones, técnicas, usuario
      administrador y recursos se resuelven por nombre/email en runtime.
      Ante ambigüedad o faltante → THROW y no se escribe nada.
   3. Transacción única (BEGIN TRAN … COMMIT/ROLLBACK) con XACT_ABORT ON.
   4. Validación de duplicados ANTES de escribir (nombres normalizados, videos
      repetidos, video ya asociado a otra técnica, alias en conflicto).
   5. Respaldo previo verificable (BACKUP COPY_ONLY + CHECKSUM y
      RESTORE VERIFYONLY). Sin COMPRESSION: la instancia (localdb) es SQL
      Server Express, que no soporta compresión de respaldos (Msg 1844).
   6. Auditoría de TODOS los cambios en AuditLogs dentro de la misma transacción.
   7. Comprobaciones de integridad posteriores al COMMIT.

   MODO SIMULACIÓN
   ---------------
   @ModoSimulacion = 1 → ejecuta todo y hace ROLLBACK (no persiste nada).
   @ModoSimulacion = 0 → COMMIT.

   Requisito externo: la carpeta de respaldos debe existir ANTES de ejecutar
       C:\Users\naiqu\Documents\MiCaminoBJJ\doc\backups
   (SQL Server / LocalDB no crea carpetas).

   DECISIONES RESPETADAS
   ---------------------
   · Las 18 técnicas usan el nombre del PDF como título principal.
   · Las 11 técnicas existentes se conservan: 4 se renombran + cambian de
     cinturón (nombre anterior → alias) y 7 quedan como registros
     administrativos sin video, ocultas de la biblioteca pública.
   · "Escape de control lateral recuperando guardia" se crea como técnica NUEVA
     sin reemplazar "Puente y giro desde control lateral".
   · Se crean las 3 posiciones faltantes: Guardia abierta, Guardia mariposa,
     Control de piernas.
   · Se reutilizan MediaAssets + TechniqueMedia (1 video exclusivo por técnica).
   · No se elimina ninguna técnica existente.
   ===================================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

/* ─────────────────────────── PARÁMETROS ─────────────────────────── */

DECLARE @ModoSimulacion      bit = 1;   -- 1 = simula y revierte | 0 = confirma
                                        -- (2026-11-12: la importación real ÚNICA ya se
                                        --  ejecutó con 0; queda 1 para no repetir escrituras)
DECLARE @EjecutarRespaldo    bit = 1;   -- 1 = BACKUP + VERIFYONLY
DECLARE @AplicarOcultamiento bit = 1;   -- 1 = IsActive=0 en las 7 técnicas sin video
DECLARE @CarpetaRespaldo     nvarchar(400) =
        N'C:\Users\naiqu\Documents\MiCaminoBJJ\doc\backups';
DECLARE @BaseEsperada        sysname = N'MiCaminoBJJ_Web_TFI_2026';
DECLARE @Actor               nvarchar(100) = N'importacion-18-tecnicas-pdf';
DECLARE @EmailAdmin          nvarchar(300) = N'admin@micaminobjj.com';

/* ─────────────────────────── 0. GUARDIAS DE ENTORNO ─────────────────────────── */

IF DB_NAME() <> @BaseEsperada
    THROW 51000, 'Base incorrecta: la importacion solo puede ejecutarse sobre MiCaminoBJJ_Web_TFI_2026.', 1;

IF OBJECT_ID(N'dbo.Techniques')          IS NULL
   OR OBJECT_ID(N'dbo.TechniqueMedia')   IS NULL
   OR OBJECT_ID(N'dbo.MediaAssets')      IS NULL
   OR OBJECT_ID(N'dbo.Positions')        IS NULL
   OR OBJECT_ID(N'dbo.TechniqueAliases') IS NULL
   OR OBJECT_ID(N'dbo.AuditLogs')        IS NULL
   OR OBJECT_ID(N'dbo.Users')            IS NULL
   OR OBJECT_ID(N'dbo.AspNetUsers')      IS NULL
   OR OBJECT_ID(N'dbo.AspNetRoles')      IS NULL
   OR OBJECT_ID(N'dbo.AspNetUserRoles')  IS NULL
    THROW 51001, 'Esquema incompleto: faltan tablas del dominio/identity esperadas en esta base.', 1;

/* ─────────────────────────── 1. RESPALDO PREVIO VERIFICABLE ─────────────────────────── */

IF @EjecutarRespaldo = 1
BEGIN
    DECLARE @SelloTiempo nvarchar(20) = FORMAT(SYSDATETIME(), 'yyyyMMdd_HHmmss');
    DECLARE @ArchivoRespaldo nvarchar(600) =
            @CarpetaRespaldo + N'\MiCaminoBJJ_Web_TFI_2026_' + @SelloTiempo + N'_PRE_IMPORT.bak';

    PRINT '=== BACKUP: ' + @ArchivoRespaldo;

    BEGIN TRY
        BACKUP DATABASE [MiCaminoBJJ_Web_TFI_2026]
            TO DISK = @ArchivoRespaldo
            WITH COPY_ONLY, INIT, CHECKSUM,
                 NAME = N'MiCaminoBJJ Web - respaldo previo a importacion de 18 tecnicas',
                 DESCRIPTION = N'Respaldo COPY_ONLY previo a la importacion del catalogo de 18 tecnicas (TecnicasYLinks.pdf).';

        RESTORE VERIFYONLY FROM DISK = @ArchivoRespaldo WITH CHECKSUM;
        PRINT '=== BACKUP VERIFICADO (RESTORE VERIFYONLY OK).';
    END TRY
    BEGIN CATCH
        THROW 51002,
              'No se pudo generar o verificar el respaldo previo. Verifique que doc\backups exista y sea escribible. Importacion cancelada sin escribir datos.',
              1;
    END CATCH
END
ELSE
    PRINT '=== BACKUP OMITIDO por parametro (@EjecutarRespaldo = 0).';

/* ─────────────────────────── 2. CATÁLOGO OBJETIVO (18 filas) ───────────────────────────
   Accion: 'RENOMBRAR' = la técnica ya existe (NombreAnterior) → se ajusta nombre
           y cinturón; el nombre anterior queda como alias.
           'CREAR'     = técnica nueva.
   -------------------------------------------------------------------------------------- */

/* Tamaños alineados a las columnas reales de dbo.Techniques:
   Name 200 chars, Description 2000, Objective 1000, Steps/Tips 4000. */
CREATE TABLE #Catalogo
(
    Clave          tinyint        NOT NULL PRIMARY KEY,
    Accion         varchar(10)    NOT NULL,
    Nombre         nvarchar(200)  NOT NULL,
    NombreAnterior nvarchar(200)  NULL,
    Cinturon       nvarchar(50)   NOT NULL,
    Categoria      nvarchar(100)  NOT NULL,
    Posicion       nvarchar(200)  NOT NULL,
    VideoId        nvarchar(20)   NOT NULL,
    VideoUrl       nvarchar(500)  NOT NULL,
    Descripcion    nvarchar(2000) NULL,
    Objetivo       nvarchar(1000) NULL,
    Pasos          nvarchar(4000) NULL,
    Consejos       nvarchar(4000) NULL
);

INSERT INTO #Catalogo
    (Clave, Accion, Nombre, NombreAnterior, Cinturon, Categoria, Posicion, VideoId, VideoUrl,
     Descripcion, Objetivo, Pasos, Consejos)
VALUES
/* ── AZUL (3) ── */
(1, 'RENOMBRAR', N'Escape de montada con codos', N'Escape de montada',
    N'Azul', N'Escape', N'Montada', N'ynSK1oYc_gg',
    N'https://youtube.com/shorts/ynSK1oYc_gg', NULL, NULL, NULL, NULL),

(2, 'CREAR', N'Escape de armbar hacia pase de guardia', NULL,
    N'Azul', N'Escape', N'Guardia cerrada', N'yLGmZxSf2Vo',
    N'https://youtu.be/yLGmZxSf2Vo',
    N'Defensa ante un intento de armbar (llave de brazo) que evita la finalizacion y transforma la situacion defensiva en una oportunidad para avanzar hacia el pase de guardia.',
    N'Reconocer la amenaza de llave de brazo, proteger el brazo, controlar la postura del oponente y continuar hacia una posicion de mayor control.',
    N'["Reconocer la amenaza de armbar manteniendo el brazo flexionado y pegado al cuerpo.","Controlar la postura del oponente para evitar que cierre el angulo.","Girar y liberar el brazo usando la cadera, sin extenderlo.","Aprovechar el espacio generado para avanzar y comenzar el pase de guardia."]',
    N'["No tires del brazo con fuerza: gira el cuerpo y usa la cadera.","Protege el codo antes de pensar en pasar.","Practica primero el reconocimiento de la amenaza, despues la velocidad."]'),

(3, 'CREAR', N'Escape de control lateral recuperando guardia', NULL,
    N'Azul', N'Escape', N'Control lateral', N'eDUjKxV6wb0',
    N'https://youtu.be/eDUjKxV6wb0',
    N'Salida desde control lateral que utiliza la estructura defensiva, la movilidad de cadera y la creacion de espacio para recuperar la guardia.',
    N'Sobrevivir al control lateral, proteger el cuello, generar espacio con la cadera y recuperar la guardia mejorando el control de la distancia.',
    N'["Proteger el cuello y mantener los codos cerca del torso.","Colocar el antebrazo como estructura en la cadera del oponente.","Mover la cadera para crear espacio y meter la rodilla.","Recuperar la guardia y controlar la distancia."]',
    N'["No quedes completamente plano: busca el costado.","El espacio se crea con la cadera, no empujando con los brazos.","Primero sobrevive y estabiliza; despues escapa."]'),

/* ── VIOLETA (6) ── */
(4, 'RENOMBRAR', N'Armbar clásico desde guardia cerrada', N'Armbar desde guardia cerrada',
    N'Violeta', N'Sumisión', N'Guardia cerrada', N'QY19oOtQPnw',
    N'https://youtu.be/QY19oOtQPnw', NULL, NULL, NULL, NULL),

(5, 'CREAR', N'Barrido clásico de mariposa', NULL,
    N'Violeta', N'Guardia', N'Guardia mariposa', N'D_dVjIm-hlI',
    N'https://youtu.be/D_dVjIm-hlI',
    N'Barrido clásico desde guardia mariposa que utiliza los ganchos, el control del equilibrio y el movimiento corporal para invertir la posicion del oponente.',
    N'Reconocer la guardia mariposa, comprender la funcion de los ganchos y desequilibrar al oponente para barrer hacia una posicion superior.',
    N'["Establecer los ganchos con los pies en la cara interna de los muslos del oponente.","Controlar la parte superior del cuerpo y la postura.","Elevar y desequilibrar con el gancho y la cadera.","Completar el barrido y estabilizar la posicion superior."]',
    N'["El gancho guia; la cadera hace el trabajo.","Manten el control de la postura durante todo el movimiento.","Coordina pierna y torso: no son movimientos separados."]'),

(6, 'CREAR', N'Barrido de cadera / Hip Bump', NULL,
    N'Violeta', N'Guardia', N'Guardia cerrada', N'jsB4Iv8vgAM',
    N'https://youtu.be/jsB4Iv8vgAM',
    N'Barrido de cadera (Hip Bump) desde guardia cerrada que aprovecha el movimiento del torso y el desequilibrio del oponente para invertir la posicion.',
    N'Reconocer cuando elevar el torso y movilizar la cadera para desequilibrar al oponente y obtener una posicion superior.',
    N'["Reconocer el momento en que el oponente descarga el peso hacia adelante.","Elevar el torso y apoyar el peso en una mano.","Rotar la cadera y llevar el brazo por encima del hombro.","Finalizar el barrido y estabilizar el control superior."]',
    N'["El barrido depende del timing, no de la fuerza.","Si el oponente se retira, vuelve a la guardia cerrada.","Manten el pecho cerca para no perder el control."]'),

(7, 'CREAR', N'Barrido de tijera', NULL,
    N'Violeta', N'Guardia', N'Guardia cerrada', N'O3KBgF9umwQ',
    N'https://youtu.be/O3KBgF9umwQ',
    N'Barrido de tijera que combina el control de la parte superior del cuerpo, el posicionamiento de las piernas y el desequilibrio para invertir la posicion.',
    N'Controlar la postura del oponente y coordinar las piernas para barrerlo hacia una posicion superior estable.',
    N'["Controlar la manga y el cuello para fijar la postura del oponente.","Colocar una pierna en la cadera y la otra atravesada sobre el torso.","Desequilibrar y cerrar las piernas como una tijera.","Rotar el torso y finalizar en una posicion superior estable."]',
    N'["Sin control de la postura, el barrido no funciona.","Las piernas se coordinan: una empuja y la otra barre.","Estabiliza antes de buscar el siguiente ataque."]'),

(8, 'CREAR', N'Recuperación de guardia desde media guardia', NULL,
    N'Violeta', N'Guardia', N'Media guardia', N'sCmv4Jc0qlU',
    N'https://youtu.be/sCmv4Jc0qlU',
    N'Recuperacion de guardia desde media guardia utilizando la estructura defensiva, el movimiento de cadera y el control de la distancia.',
    N'Proteger la estructura corporal, generar espacio con la cadera y volver a una guardia mas estable.',
    N'["Reconocer la media guardia y proteger el cuello y el brazo atrapado.","Mantener una estructura segura con la rodilla y el antebrazo.","Crear espacio con la cadera y recuperar la pierna.","Volver a la guardia cerrada o abierta con control de la distancia."]',
    N'["No permitas que te aplanen: manten el costado.","El control de la distancia evita que el oponente avance.","Practica primero el orden de las estructuras."]'),

(9, 'RENOMBRAR', N'Triángulo clásico desde guardia cerrada', N'Triángulo desde guardia cerrada',
    N'Violeta', N'Sumisión', N'Guardia cerrada', N'7A72vo39F3Q',
    N'https://youtu.be/7A72vo39F3Q', NULL, NULL, NULL, NULL),

/* ── MARRÓN (5) ── */
(10, 'CREAR', N'Pase de media guardia con pierna recta', NULL,
    N'Marrón', N'Control', N'Media guardia', N'ZEIcluOsrL8',
    N'https://youtu.be/ZEIcluOsrL8',
    N'Pase de media guardia que utiliza el posicionamiento corporal, el control del oponente y el movimiento de la pierna para superar la defensa y avanzar.',
    N'Reconocer la media guardia desde arriba, mantener equilibrio y presion controlada, liberar la pierna atrapada y avanzar hacia una posicion de control superior.',
    N'["Estabilizar la base y reconocer la media guardia desde arriba.","Controlar la cadera y la parte superior del cuerpo del oponente.","Liberar la pierna atrapada con un movimiento controlado.","Avanzar hacia el control lateral manteniendo la presion."]',
    N'["La base estable va antes que la velocidad.","No apliques presion brusca sobre la rodilla del companero.","Controla la cadera antes de liberar la pierna."]'),

(11, 'RENOMBRAR', N'Pase con corte de rodilla / Knee Cut', N'Pass de corte de rodilla',
    N'Marrón', N'Control', N'Control lateral', N'7fFBZ7DgKs4',
    N'https://youtu.be/7fFBZ7DgKs4', NULL, NULL, NULL, NULL),

(12, 'CREAR', N'Pase toreando', NULL,
    N'Marrón', N'Control', N'Guardia abierta', N'ZGStoRHimbE',
    N'https://youtu.be/ZGStoRHimbE',
    N'Pase toreando para superar la guardia abierta mediante el control de las piernas, el desplazamiento lateral y la coordinacion corporal.',
    N'Reconocer las oportunidades sobre guardia abierta, controlar las piernas, desplazarse lateralmente y avanzar hacia una posicion dominante.',
    N'["Asegurar el control de las piernas del oponente.","Desplazar el cuerpo lateralmente para desviar la guardia.","Pasar las piernas manteniendo el control de la cadera.","Estabilizar el control lateral y ajustar la base."]',
    N'["El desplazamiento lateral es la clave del pase.","Manten el control de las piernas mientras avanzas.","Respeta la movilidad y la seguridad de tu companero."]'),

(13, 'CREAR', N'Pase doble por debajo', NULL,
    N'Marrón', N'Control', N'Guardia abierta', N'zrUofVX5Xgk',
    N'https://youtu.be/zrUofVX5Xgk',
    N'Pase doble por debajo que controla ambas piernas y utiliza el posicionamiento corporal para limitar la movilidad del oponente y avanzar.',
    N'Reconocer sus caracteristicas, controlar ambas piernas, mantener una postura estable y avanzar hacia una posicion superior segura.',
    N'["Controlar ambas piernas y acercarlas al torso.","Mantener la postura estable mientras avanzas.","Pasar por debajo con la cadera y el pecho.","Estabilizar la posicion superior con control."]',
    N'["Evita presion excesiva sobre el cuello y la columna.","La postura estable evita que te barran durante el pase.","Avanza progresivamente, sin tirones."]'),

(14, 'CREAR', N'Apertura clásica de guardia cerrada de rodillas', NULL,
    N'Marrón', N'Control', N'Guardia cerrada', N'GK7g0oSZWvU',
    N'https://youtube.com/shorts/GK7g0oSZWvU',
    N'Apertura tradicional de guardia cerrada desde una posicion arrodillada, usando control de la postura, base estable y posicionamiento corporal.',
    N'Reconocer la guardia cerrada desde arriba, conservar una postura estable, generar espacio para abrir la guardia e identificar la oportunidad de pase.',
    N'["Conservar una postura estable con las manos controlando la cadera.","Apoyar una rodilla y generar espacio para abrir.","Abrir la guardia de forma controlada.","Transicionar hacia la oportunidad de pase."]',
    N'["No hagas movimientos bruscos sobre las piernas del companero.","La base estable evita que te desequilibren.","Abre la guardia y continua de inmediato: no regales tiempo."]'),

/* ── NEGRO (4) ── */
(15, 'CREAR', N'Llave recta de tobillo', NULL,
    N'Negro', N'Sumisión', N'Control de piernas', N'9KvpsjNfHNk',
    N'https://youtu.be/9KvpsjNfHNk',
    N'Llave recta de tobillo (Straight Ankle Lock): tecnica de sumision que utiliza el control de la pierna y una presion controlada sobre la articulacion del tobillo.',
    N'Reconocer la posicion y el objetivo de la tecnica, comprender la importancia del control de la pierna y aplicar presion gradual y controlada.',
    N'["Reconocer la posicion y el objetivo de la tecnica.","Controlar la pierna con el brazo y la axila.","Ajustar la alineacion del tobillo y la presion.","Aplicar presion gradual y liberar ante la senal de rendicion."]',
    N'["Presion gradual: nunca brusca.","Libera de inmediato ante cualquier senal de rendicion.","Verifica el reglamento de tu categoria antes de practicarla."]'),

(16, 'CREAR', N'Armbar desde montada', NULL,
    N'Negro', N'Sumisión', N'Montada', N'VToH_xYHFk0',
    N'https://youtu.be/VToH_xYHFk0',
    N'Armbar desde montada: tecnica de sumision que busca controlar el brazo del oponente y aplicar una llave sobre la articulacion del codo desde la posicion dominante.',
    N'Reconocer la montada y sus posibilidades de ataque, controlar el brazo, mantener el equilibrio y aplicar la llave con control.',
    N'["Reconocer la montada y sus posibilidades de ataque.","Controlar el brazo y mantener el equilibrio corporal.","Girar la cadera y ajustar la posicion del cuerpo.","Extender la cadera con control y liberar ante la senal de rendicion."]',
    N'["El equilibrio se mantiene con la base, no con la fuerza.","Aplica la llave lentamente.","Libera apenas el companero rinda."]'),

(17, 'CREAR', N'Americana desde montada', NULL,
    N'Negro', N'Sumisión', N'Montada', N'NcyJYHf64wg',
    N'https://youtu.be/NcyJYHf64wg',
    N'Americana desde montada: tecnica de sumision que utiliza el control del brazo y la rotacion del hombro para aplicar una llave articular.',
    N'Identificar la montada como punto de partida, mantener una base estable, controlar el brazo y observar el posicionamiento corporal necesario.',
    N'["Identificar la montada como punto de partida.","Mantener una base estable con las rodillas.","Controlar el brazo y fijar la mano al suelo.","Rotar el hombro de forma gradual y controlada."]',
    N'["Practica lentamente y sin movimientos bruscos.","La base estable es lo que permite la llave.","Respeta los limites y la rendicion del companero."]'),

(18, 'CREAR', N'Kimura desde control lateral', NULL,
    N'Negro', N'Sumisión', N'Control lateral', N'AXzy8QA4bv0',
    N'https://youtu.be/AXzy8QA4bv0',
    N'Kimura desde control lateral: tecnica de sumision que combina el control del brazo con una llave de rotacion sobre la articulacion del hombro.',
    N'Reconocer el control lateral como posicion dominante, controlar el brazo del oponente y mantener estabilidad durante la transicion hacia la sumision.',
    N'["Reconocer el control lateral como posicion dominante.","Controlar el brazo del oponente y fijar el codo.","Ajustar la rotacion del hombro con el cuerpo.","Aplicar presion gradual y liberar ante la rendicion."]',
    N'["La estabilidad se sostiene con el pecho y la cadera.","No gires el brazo de golpe.","Libera de inmediato ante la senal de rendicion."]');

/* NombreNormalizado explícito: mismo resultado que TextNormalizer.Normalize
   (mayúsculas, sin diacríticos —incluida ñ→N— y espacios simples). Se escriben
   los valores ya calculados y verificados para no depender de REPLACE anidados. */
ALTER TABLE #Catalogo ADD NombreNormalizado nvarchar(400) NULL;
ALTER TABLE #Catalogo ADD NombreAnteriorNormalizado nvarchar(400) NULL;

UPDATE c
SET c.NombreNormalizado = v.Norm
FROM #Catalogo c
JOIN (VALUES
        (1,  N'ESCAPE DE MONTADA CON CODOS'),
        (2,  N'ESCAPE DE ARMBAR HACIA PASE DE GUARDIA'),
        (3,  N'ESCAPE DE CONTROL LATERAL RECUPERANDO GUARDIA'),
        (4,  N'ARMBAR CLASICO DESDE GUARDIA CERRADA'),
        (5,  N'BARRIDO CLASICO DE MARIPOSA'),
        (6,  N'BARRIDO DE CADERA / HIP BUMP'),
        (7,  N'BARRIDO DE TIJERA'),
        (8,  N'RECUPERACION DE GUARDIA DESDE MEDIA GUARDIA'),
        (9,  N'TRIANGULO CLASICO DESDE GUARDIA CERRADA'),
        (10, N'PASE DE MEDIA GUARDIA CON PIERNA RECTA'),
        (11, N'PASE CON CORTE DE RODILLA / KNEE CUT'),
        (12, N'PASE TOREANDO'),
        (13, N'PASE DOBLE POR DEBAJO'),
        (14, N'APERTURA CLASICA DE GUARDIA CERRADA DE RODILLAS'),
        (15, N'LLAVE RECTA DE TOBILLO'),
        (16, N'ARMBAR DESDE MONTADA'),
        (17, N'AMERICANA DESDE MONTADA'),
        (18, N'KIMURA DESDE CONTROL LATERAL')
     ) AS v (Clave, Norm) ON v.Clave = c.Clave;

/* Normalización del nombre anterior (solo las 4 que se renombran). */
UPDATE c
SET c.NombreAnteriorNormalizado = v.Norm
FROM #Catalogo c
JOIN (VALUES
        (1,  N'ESCAPE DE MONTADA'),
        (4,  N'ARMBAR DESDE GUARDIA CERRADA'),
        (9,  N'TRIANGULO DESDE GUARDIA CERRADA'),
        (11, N'PASS DE CORTE DE RODILLA')
     ) AS v (Clave, Norm) ON v.Clave = c.Clave;

IF EXISTS (SELECT 1 FROM #Catalogo
           WHERE NombreNormalizado IS NULL
              OR (Accion = 'RENOMBRAR' AND NombreAnteriorNormalizado IS NULL))
    THROW 51099, 'Faltan valores normalizados en el catalogo. Revision previa requerida.', 1;

/* Técnicas existentes SIN video que quedarán como registros administrativos. */
CREATE TABLE #SinVideoPublicacion (Nombre nvarchar(400) NOT NULL PRIMARY KEY);

INSERT INTO #SinVideoPublicacion (Nombre) VALUES
    (N'Mata león'),
    (N'Posicionamiento de supervivencia con control lateral'),
    (N'Raspado básico'),
    (N'Defensa de kimura en control lateral'),
    (N'Kimura desde guardia cerrada'),
    (N'Puente y giro desde control lateral'),
    (N'Estrangulación con collar en control lateral');

/* ─────────────────────────── 3. VALIDACIONES PREVIAS (sin escribir) ─────────────────────────── */

IF (SELECT COUNT(*) FROM #Catalogo) <> 18
    THROW 51100, 'El catalogo debe contener exactamente 18 tecnicas.', 1;

IF EXISTS (SELECT VideoId FROM #Catalogo GROUP BY VideoId HAVING COUNT(*) > 1)
    THROW 51101, 'Hay IDs de video repetidos: cada video debe pertenecer a una sola tecnica.', 1;

IF EXISTS (SELECT NombreNormalizado FROM #Catalogo GROUP BY NombreNormalizado HAVING COUNT(*) > 1)
    THROW 51102, 'Hay nombres normalizados repetidos en el catalogo objetivo.', 1;

IF (SELECT COUNT(*) FROM #Catalogo WHERE Accion = 'RENOMBRAR') <> 4
    THROW 51103, 'Se esperaban exactamente 4 tecnicas existentes a renombrar.', 1;

IF (SELECT COUNT(*) FROM #Catalogo WHERE Accion = 'CREAR') <> 14
    THROW 51104, 'Se esperaban exactamente 14 tecnicas nuevas.', 1;

/* 3.1 Las 4 a renombrar existen (por nombre anterior, o ya renombradas en una
       ejecución previa: idempotencia) y son únicas. */
IF EXISTS (
    SELECT 1 FROM #Catalogo c
    WHERE c.Accion = 'RENOMBRAR'
      AND NOT EXISTS (SELECT 1 FROM dbo.Techniques t
                      WHERE t.NameNormalized = c.NombreNormalizado)
      AND NOT EXISTS (SELECT 1 FROM dbo.Techniques t
                      WHERE t.Name COLLATE Latin1_General_CI_AI = c.NombreAnterior COLLATE Latin1_General_CI_AI))
    THROW 51105, 'Falta alguna de las tecnicas existentes a renombrar (revisar nombres anteriores).', 1;

IF EXISTS (
    SELECT 1 FROM #Catalogo c
    WHERE c.Accion = 'RENOMBRAR'
      AND (SELECT COUNT(*) FROM dbo.Techniques t
           WHERE t.Name COLLATE Latin1_General_CI_AI = c.NombreAnterior COLLATE Latin1_General_CI_AI) > 1)
    THROW 51106, 'Hay mas de una tecnica con el nombre anterior: resolucion ambigua, no se escribe nada.', 1;

/* 3.1 bis Una técnica renombrada NO debe coexistir con otra que ya tenga el
          nombre nuevo (evita duplicados por importaciones parciales). */
IF EXISTS (
    SELECT c.NombreNormalizado
    FROM #Catalogo c
    JOIN dbo.Techniques t ON t.NameNormalized = c.NombreNormalizado
    GROUP BY c.NombreNormalizado
    HAVING COUNT(*) > 1)
    THROW 51115, 'Existe mas de una tecnica con el nombre nuevo del catalogo. Resolver manualmente.', 1;

/* 3.2 Los 7 registros administrativos existen. */
IF EXISTS (
    SELECT 1 FROM #SinVideoPublicacion s
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Techniques t
                      WHERE t.Name COLLATE Latin1_General_CI_AI = s.Nombre COLLATE Latin1_General_CI_AI))
    THROW 51107, 'Falta alguna de las 7 tecnicas administrativas sin video esperadas.', 1;

/* 3.3 Ningún nombre nuevo puede chocar con un ALIAS existente de otra técnica. */
IF EXISTS (
    SELECT 1 FROM #Catalogo c
    JOIN dbo.TechniqueAliases a ON a.NameNormalized = c.NombreNormalizado
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Techniques t
                      WHERE t.Id = a.TechniqueId
                        AND t.NameNormalized = c.NombreNormalizado))
    THROW 51108, 'Un alias existente coincide con el nombre de una tecnica nueva. Resolver manualmente antes de importar.', 1;

/* 3.4 Ningún video del catálogo puede estar ya asociado a OTRA técnica. */
IF EXISTS (
    SELECT 1
    FROM #Catalogo c
    JOIN dbo.MediaAssets    ma ON ma.ExternalVideoId = c.VideoId AND ma.IsDeleted = 0
    JOIN dbo.TechniqueMedia tm ON tm.MediaAssetId = ma.Id
    JOIN dbo.Techniques     t  ON t.Id = tm.TechniqueId
    WHERE NOT (
            (c.Accion = 'RENOMBRAR'
             AND (t.NameNormalized = c.NombreNormalizado
                  OR t.Name COLLATE Latin1_General_CI_AI = c.NombreAnterior COLLATE Latin1_General_CI_AI))
         OR (c.Accion = 'CREAR' AND t.NameNormalized = c.NombreNormalizado)))
    THROW 51109, 'Algun video del catalogo ya esta asociado a una tecnica distinta. Importacion cancelada.', 1;

/* 3.5 Usuario administrador REAL (autor de los recursos). */
IF (SELECT COUNT(*) FROM dbo.Users
    WHERE Email COLLATE Latin1_General_CI_AI = @EmailAdmin COLLATE Latin1_General_CI_AI) <> 1
    THROW 51110, 'El usuario de dominio admin@micaminobjj.com no existe o esta duplicado. Importacion cancelada.', 1;

DECLARE @AdminUserId int =
    (SELECT u.Id FROM dbo.Users u
     WHERE u.Email COLLATE Latin1_General_CI_AI = @EmailAdmin COLLATE Latin1_General_CI_AI);

IF NOT EXISTS (
    SELECT 1
    FROM dbo.AspNetUsers au
    JOIN dbo.AspNetUserRoles ur ON ur.UserId = au.Id
    JOIN dbo.AspNetRoles     r  ON r.Id = ur.RoleId
    WHERE au.DomainUserId = @AdminUserId
      AND r.Name = N'Administrador')
    THROW 51111, 'El usuario admin@micaminobjj.com no tiene el rol Administrador en Identity. Importacion cancelada.', 1;

PRINT '=== Administrador autor resuelto (UserId real): ' + CONVERT(varchar(20), @AdminUserId);

/* 3.6 Cinturones y categorías requeridos existen. */
IF EXISTS (SELECT 1 FROM #Catalogo c
           WHERE NOT EXISTS (SELECT 1 FROM dbo.Belts b
                             WHERE b.Name COLLATE Latin1_General_CI_AI = c.Cinturon COLLATE Latin1_General_CI_AI))
    THROW 51112, 'Falta algun cinturon requerido en Belts.', 1;

IF EXISTS (SELECT 1 FROM #Catalogo c
           WHERE NOT EXISTS (SELECT 1 FROM dbo.TechniqueCategories tc
                             WHERE tc.Name COLLATE Latin1_General_CI_AI = c.Categoria COLLATE Latin1_General_CI_AI))
    THROW 51113, 'Falta alguna categoria requerida en TechniqueCategories.', 1;

/* 3.7 Posiciones: solo se autoriza crear las 3 nuevas previstas. */
IF EXISTS (
    SELECT 1 FROM (SELECT DISTINCT Posicion FROM #Catalogo) c
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Positions p
                      WHERE p.Name COLLATE Latin1_General_CI_AI = c.Posicion COLLATE Latin1_General_CI_AI)
      AND c.Posicion NOT IN (N'Guardia abierta', N'Guardia mariposa', N'Control de piernas'))
    THROW 51114, 'El catalogo referencia una posicion inexistente no autorizada para crear.', 1;

/* Estado previo (para el informe). */
DECLARE @AntesTecnicas       int = (SELECT COUNT(*) FROM dbo.Techniques);
DECLARE @AntesPosiciones     int = (SELECT COUNT(*) FROM dbo.Positions);
DECLARE @AntesAlias          int = (SELECT COUNT(*) FROM dbo.TechniqueAliases);
DECLARE @AntesMedia          int = (SELECT COUNT(*) FROM dbo.MediaAssets);
DECLARE @AntesTechniqueMedia int = (SELECT COUNT(*) FROM dbo.TechniqueMedia);
DECLARE @AntesAudit          int = (SELECT COUNT(*) FROM dbo.AuditLogs);

PRINT '=== Estado previo: Techniques='      + CONVERT(varchar(10), @AntesTecnicas)
    + ' Positions='      + CONVERT(varchar(10), @AntesPosiciones)
    + ' Aliases='        + CONVERT(varchar(10), @AntesAlias)
    + ' MediaAssets='    + CONVERT(varchar(10), @AntesMedia)
    + ' TechniqueMedia=' + CONVERT(varchar(10), @AntesTechniqueMedia)
    + ' AuditLogs='      + CONVERT(varchar(10), @AntesAudit);

/* ─────────────────────────── 4. TRANSACCIÓN ÚNICA ─────────────────────────── */

DECLARE @NuevasPosiciones TABLE (Id int NOT NULL, Nombre nvarchar(200) NOT NULL, Orden int NOT NULL);
DECLARE @Renombradas TABLE
(
    TechniqueId int NOT NULL,
    NombreNuevo nvarchar(200) NOT NULL,
    NombreViejo nvarchar(200) NOT NULL,
    BeltIdViejo int NOT NULL,
    BeltIdNuevo int NOT NULL
);
DECLARE @TecnicasCreadas  TABLE (Id int NOT NULL PRIMARY KEY);
DECLARE @TecnicasObjetivo TABLE (Clave tinyint NOT NULL PRIMARY KEY, TechniqueId int NOT NULL);
DECLARE @NuevosAlias      TABLE (Id int NOT NULL PRIMARY KEY);
DECLARE @NuevosMedia      TABLE (Id int NOT NULL PRIMARY KEY);
DECLARE @NuevosVinculos   TABLE (Id int NOT NULL PRIMARY KEY);
DECLARE @Ocultadas        TABLE (TechniqueId int NOT NULL PRIMARY KEY);

BEGIN TRY
    BEGIN TRANSACTION;

    /* ── 4.1 Posiciones faltantes (idempotente) ── */
    DECLARE @MaxOrdenPos int = (SELECT ISNULL(MAX(DisplayOrder), 0) FROM dbo.Positions);

    INSERT INTO dbo.Positions (Name, DisplayOrder, Description, CreatedAt)
    OUTPUT inserted.Id, inserted.Name, inserted.DisplayOrder INTO @NuevasPosiciones (Id, Nombre, Orden)
    SELECT x.Nombre, @MaxOrdenPos + x.Orden, x.Descripcion, SYSUTCDATETIME()
    FROM (VALUES
            (N'Guardia abierta',    1, N'Guardia con las piernas abiertas y los pies como estructura, sin cerrar los tobillos.'),
            (N'Guardia mariposa',   2, N'Guardia sentada con ganchos en la cara interna de los muslos del oponente.'),
            (N'Control de piernas', 3, N'Control de las piernas del oponente, base de las sumisiones de pierna.')
         ) AS x (Nombre, Orden, Descripcion)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Positions p
                      WHERE p.Name COLLATE Latin1_General_CI_AI = x.Nombre COLLATE Latin1_General_CI_AI);

    DECLARE @nPosiciones int = (SELECT COUNT(*) FROM @NuevasPosiciones);
    PRINT '=== Posiciones creadas: ' + CONVERT(varchar(10), @nPosiciones);

    /* ── 4.2 Renombrado + corrección de cinturón (4 técnicas existentes) ── */
    UPDATE t
    SET t.Name           = c.Nombre,
        t.NameNormalized = c.NombreNormalizado,
        t.BeltId         = b.Id,
        t.UpdatedAt      = SYSUTCDATETIME()
    OUTPUT inserted.Id, inserted.Name, deleted.Name, deleted.BeltId, inserted.BeltId
        INTO @Renombradas (TechniqueId, NombreNuevo, NombreViejo, BeltIdViejo, BeltIdNuevo)
    FROM dbo.Techniques t
    JOIN #Catalogo c
         ON c.Accion = 'RENOMBRAR'
        AND t.Name COLLATE Latin1_General_CI_AI = c.NombreAnterior COLLATE Latin1_General_CI_AI
    JOIN dbo.Belts b
         ON b.Name COLLATE Latin1_General_CI_AI = c.Cinturon COLLATE Latin1_General_CI_AI
    WHERE t.Name <> c.Nombre
       OR t.BeltId <> b.Id;

    DECLARE @nRenombradas int = (SELECT COUNT(*) FROM @Renombradas);
    PRINT '=== Tecnicas renombradas / recinturonadas: ' + CONVERT(varchar(10), @nRenombradas);

    /* ── 4.3 Alta de las 14 técnicas nuevas (idempotente) ── */
    INSERT INTO dbo.Techniques
        (Name, NameNormalized, Description, Objective, Steps, Tips,
         BeltId, CategoryId, PositionId, IsActive, CreatedAt, UpdatedAt)
    OUTPUT inserted.Id INTO @TecnicasCreadas (Id)
    SELECT c.Nombre,
           c.NombreNormalizado,
           c.Descripcion,
           c.Objetivo,
           c.Pasos,
           c.Consejos,
           b.Id,
           cat.Id,
           p.Id,
           1,
           SYSUTCDATETIME(),
           NULL
    FROM #Catalogo c
    JOIN dbo.Belts               b   ON b.Name   COLLATE Latin1_General_CI_AI = c.Cinturon  COLLATE Latin1_General_CI_AI
    JOIN dbo.TechniqueCategories cat ON cat.Name COLLATE Latin1_General_CI_AI = c.Categoria COLLATE Latin1_General_CI_AI
    JOIN dbo.Positions           p   ON p.Name   COLLATE Latin1_General_CI_AI = c.Posicion  COLLATE Latin1_General_CI_AI
    WHERE c.Accion = 'CREAR'
      AND NOT EXISTS (SELECT 1 FROM dbo.Techniques t WHERE t.NameNormalized = c.NombreNormalizado);

    DECLARE @nTecnicasCreadas int = (SELECT COUNT(*) FROM @TecnicasCreadas);
    PRINT '=== Tecnicas creadas: ' + CONVERT(varchar(10), @nTecnicasCreadas);

    /* Mapeo Clave → TechniqueId real (por nombre normalizado, nunca por Id supuesto). */
    INSERT INTO @TecnicasObjetivo (Clave, TechniqueId)
    SELECT c.Clave, t.Id
    FROM #Catalogo c
    JOIN dbo.Techniques t ON t.NameNormalized = c.NombreNormalizado;

    IF (SELECT COUNT(*) FROM @TecnicasObjetivo) <> 18
        THROW 51120, 'No se pudieron resolver las 18 tecnicas objetivo. Se revierte todo.', 1;

    /* ── 4.4 Alias: nombre anterior de las 4 renombradas (idempotente) ── */
    INSERT INTO dbo.TechniqueAliases (TechniqueId, Name, NameNormalized, CreatedAt)
    OUTPUT inserted.Id INTO @NuevosAlias (Id)
    SELECT r.TechniqueId,
           c.NombreAnterior,
           c.NombreAnteriorNormalizado,
           SYSUTCDATETIME()
    FROM @Renombradas r
    JOIN @TecnicasObjetivo o ON o.TechniqueId = r.TechniqueId
    JOIN #Catalogo c         ON c.Clave = o.Clave AND c.Accion = 'RENOMBRAR'
    WHERE NOT EXISTS (
            SELECT 1 FROM dbo.TechniqueAliases a
            WHERE a.TechniqueId = r.TechniqueId
              AND a.NameNormalized = c.NombreAnteriorNormalizado);

    DECLARE @nAliases int = (SELECT COUNT(*) FROM @NuevosAlias);
    PRINT '=== Aliases creados: ' + CONVERT(varchar(10), @nAliases);

    /* ── 4.5 MediaAssets: 18 videos de YouTube (idempotente por ExternalVideoId) ── */
    INSERT INTO dbo.MediaAssets
        (UploadedByUserId, MediaType, SourceType, LocalPath, RemoteUrl, ExternalUrl,
         ExternalProvider, ExternalVideoId, OriginalFileName, MimeType, FileSizeBytes,
         Title, Description, ThumbnailPath, SyncStatus, CreatedAt, UpdatedAt, IsDeleted)
    OUTPUT inserted.Id INTO @NuevosMedia (Id)
    SELECT @AdminUserId,
           1,          -- MediaType.Video
           3,          -- MediaSourceType.ExternalLink
           NULL, NULL,
           c.VideoUrl,
           1,          -- ExternalMediaProvider.YouTube
           c.VideoId,
           NULL, NULL, NULL,
           c.Nombre,
           COALESCE(c.Descripcion, N'Demostracion en video de ' + c.Nombre + N'.'),
           NULL,
           0,          -- SyncStatus.LocalOnly
           SYSUTCDATETIME(), NULL, 0
    FROM #Catalogo c
    WHERE NOT EXISTS (SELECT 1 FROM dbo.MediaAssets ma
                      WHERE ma.ExternalVideoId = c.VideoId AND ma.IsDeleted = 0);

    DECLARE @nMedia int = (SELECT COUNT(*) FROM @NuevosMedia);
    PRINT '=== MediaAssets creados: ' + CONVERT(varchar(10), @nMedia);

    /* ── 4.6 Vínculos TechniqueMedia (1 video por técnica, idempotente) ── */
    INSERT INTO dbo.TechniqueMedia (TechniqueId, MediaAssetId, IsCover, DisplayOrder, CreatedAt)
    OUTPUT inserted.Id INTO @NuevosVinculos (Id)
    SELECT o.TechniqueId, ma.Id, 0, 1, SYSUTCDATETIME()
    FROM #Catalogo c
    JOIN @TecnicasObjetivo o ON o.Clave = c.Clave
    JOIN dbo.MediaAssets   ma ON ma.ExternalVideoId = c.VideoId AND ma.IsDeleted = 0
    WHERE NOT EXISTS (SELECT 1 FROM dbo.TechniqueMedia tm
                      WHERE tm.TechniqueId = o.TechniqueId
                        AND tm.MediaAssetId = ma.Id);

    DECLARE @nVinculos int = (SELECT COUNT(*) FROM @NuevosVinculos);
    PRINT '=== Vinculos TechniqueMedia creados: ' + CONVERT(varchar(10), @nVinculos);

    /* ── 4.7 Ocultamiento de las 7 técnicas administrativas sin video ── */
    IF @AplicarOcultamiento = 1
    BEGIN
        UPDATE t
        SET t.IsActive = 0, t.UpdatedAt = SYSUTCDATETIME()
        OUTPUT inserted.Id INTO @Ocultadas (TechniqueId)
        FROM dbo.Techniques t
        JOIN #SinVideoPublicacion s
             ON t.Name COLLATE Latin1_General_CI_AI = s.Nombre COLLATE Latin1_General_CI_AI
        WHERE t.IsActive = 1
          AND NOT EXISTS (SELECT 1 FROM dbo.TechniqueMedia tm
                          JOIN dbo.MediaAssets ma ON ma.Id = tm.MediaAssetId AND ma.IsDeleted = 0
                          WHERE tm.TechniqueId = t.Id);
    END

    DECLARE @nOcultadas int = (SELECT COUNT(*) FROM @Ocultadas);
    PRINT '=== Tecnicas ocultadas (IsActive=0): ' + CONVERT(varchar(10), @nOcultadas);

    /* ── 4.8 Auditoría de todos los cambios (misma transacción) ── */
    /* 4.8.1 Posiciones creadas */
    INSERT INTO dbo.AuditLogs (UserId, Action, EntityType, EntityId, TimestampUtc, Result, Details, ErrorMessage)
    SELECT NULL, 0, N'Position', np.Id, SYSUTCDATETIME(), 0,
           (SELECT @Actor AS Actor, N'Create' AS Operacion, np.Nombre AS Name, np.Orden AS DisplayOrder
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
           NULL
    FROM @NuevasPosiciones np;

    /* 4.8.2 Técnicas renombradas / recinturonadas */
    INSERT INTO dbo.AuditLogs (UserId, Action, EntityType, EntityId, TimestampUtc, Result, Details, ErrorMessage)
    SELECT NULL, 1, N'Technique', r.TechniqueId, SYSUTCDATETIME(), 0,
           (SELECT @Actor AS Actor, N'RenameAndBelt' AS Operacion,
                   r.NombreViejo AS OldName, r.NombreNuevo AS Name,
                   r.BeltIdViejo AS OldBeltId, r.BeltIdNuevo AS BeltId
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
           NULL
    FROM @Renombradas r;

    /* 4.8.3 Técnicas creadas */
    INSERT INTO dbo.AuditLogs (UserId, Action, EntityType, EntityId, TimestampUtc, Result, Details, ErrorMessage)
    SELECT NULL, 0, N'Technique', t.Id, SYSUTCDATETIME(), 0,
           (SELECT @Actor AS Actor, N'Create' AS Operacion, t.Name AS Name,
                   t.BeltId AS BeltId, t.CategoryId AS CategoryId, t.PositionId AS PositionId,
                   t.IsActive AS IsActive
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
           NULL
    FROM dbo.Techniques t
    WHERE t.Id IN (SELECT Id FROM @TecnicasCreadas);

    /* 4.8.4 Aliases creados */
    INSERT INTO dbo.AuditLogs (UserId, Action, EntityType, EntityId, TimestampUtc, Result, Details, ErrorMessage)
    SELECT NULL, 0, N'TechniqueAlias', a.Id, SYSUTCDATETIME(), 0,
           (SELECT @Actor AS Actor, N'Create' AS Operacion, a.Name AS Name, a.TechniqueId AS TechniqueId
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
           NULL
    FROM dbo.TechniqueAliases a
    WHERE a.Id IN (SELECT Id FROM @NuevosAlias);

    /* 4.8.5 MediaAssets creados */
    INSERT INTO dbo.AuditLogs (UserId, Action, EntityType, EntityId, TimestampUtc, Result, Details, ErrorMessage)
    SELECT NULL, 0, N'MediaAsset', ma.Id, SYSUTCDATETIME(), 0,
           (SELECT @Actor AS Actor, N'Create' AS Operacion, ma.Title AS Title,
                   ma.ExternalVideoId AS ExternalVideoId, ma.ExternalUrl AS ExternalUrl,
                   ma.UploadedByUserId AS UploadedByUserId
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
           NULL
    FROM dbo.MediaAssets ma
    WHERE ma.Id IN (SELECT Id FROM @NuevosMedia);

    /* 4.8.6 Vínculos TechniqueMedia creados */
    INSERT INTO dbo.AuditLogs (UserId, Action, EntityType, EntityId, TimestampUtc, Result, Details, ErrorMessage)
    SELECT NULL, 0, N'TechniqueMedia', tm.Id, SYSUTCDATETIME(), 0,
           (SELECT @Actor AS Actor, N'Create' AS Operacion,
                   tm.TechniqueId AS TechniqueId, tm.MediaAssetId AS MediaAssetId
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
           NULL
    FROM dbo.TechniqueMedia tm
    WHERE tm.Id IN (SELECT Id FROM @NuevosVinculos);

    /* 4.8.7 Técnicas ocultadas (baja lógica de la biblioteca pública) */
    INSERT INTO dbo.AuditLogs (UserId, Action, EntityType, EntityId, TimestampUtc, Result, Details, ErrorMessage)
    SELECT NULL, 2 /* SoftDelete */, N'Technique', o.TechniqueId, SYSUTCDATETIME(), 0,
           (SELECT @Actor AS Actor, N'HideWithoutVideo' AS Operacion, 0 AS IsActive
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
           NULL
    FROM @Ocultadas o;

    /* ── 4.9 Resumen intra-transacción ── */
    DECLARE @nAuditImport int =
        (SELECT COUNT(*) FROM dbo.AuditLogs
         WHERE Details LIKE N'%"Actor":"' + @Actor + N'"%') - @AntesAudit;

    PRINT '--- RESUMEN DE LA IMPORTACION ---------------------------------------------';
    PRINT 'Tecnicas creadas        : ' + CONVERT(varchar(10), @nTecnicasCreadas);
    PRINT 'Tecnicas renombradas    : ' + CONVERT(varchar(10), @nRenombradas);
    PRINT 'Posiciones creadas      : ' + CONVERT(varchar(10), @nPosiciones);
    PRINT 'Aliases creados         : ' + CONVERT(varchar(10), @nAliases);
    PRINT 'MediaAssets creados     : ' + CONVERT(varchar(10), @nMedia);
    PRINT 'Vinculos TechniqueMedia : ' + CONVERT(varchar(10), @nVinculos);
    PRINT 'Tecnicas ocultadas      : ' + CONVERT(varchar(10), @nOcultadas);
    PRINT 'Auditorias creadas      : ' + CONVERT(varchar(10), @nAuditImport);
    PRINT '---------------------------------------------------------------------------';

    IF @ModoSimulacion = 1
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT '=== MODO SIMULACION: ROLLBACK ejecutado. NO se persistio ningun cambio. ===';
    END
    ELSE
    BEGIN
        COMMIT TRANSACTION;
        PRINT '=== COMMIT ejecutado. Cambios persistidos. ===';
    END
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    PRINT '=== ERROR: importacion revertida sin cambios. ===';
    THROW;
END CATCH;

/* ─────────────────────────── 5. COMPROBACIONES POSTERIORES ─────────────────────────── */

IF @ModoSimulacion = 1
BEGIN
    PRINT '=== Comprobaciones posteriores omitidas (transaccion revertida). ===';
END
ELSE
BEGIN
    PRINT '=== COMPROBACIONES DE INTEGRIDAD ===';

    /* 5.1 Distribución final por cinturón (esperado: 25 técnicas totales) */
    SELECT b.Name AS Cinturon,
           COUNT(*) AS Tecnicas,
           SUM(CASE WHEN t.IsActive = 1 THEN 1 ELSE 0 END) AS Activas
    FROM dbo.Techniques t
    JOIN dbo.Belts b ON b.Id = t.BeltId
    GROUP BY b.Name, b.DisplayOrder
    ORDER BY b.DisplayOrder;

    /* 5.2 Las 18 técnicas del PDF con cinturón y video */
    SELECT t.Id AS TechniqueId, t.Name AS Tecnica, b.Name AS Cinturon, t.IsActive,
           ma.ExternalVideoId, ma.ExternalUrl, tm.IsCover
    FROM dbo.Techniques t
    JOIN dbo.Belts b ON b.Id = t.BeltId
    LEFT JOIN dbo.TechniqueMedia tm ON tm.TechniqueId = t.Id
    LEFT JOIN dbo.MediaAssets   ma ON ma.Id = tm.MediaAssetId AND ma.IsDeleted = 0
    WHERE t.Name IN (N'Escape de montada con codos', N'Escape de armbar hacia pase de guardia',
                     N'Escape de control lateral recuperando guardia', N'Armbar clásico desde guardia cerrada',
                     N'Barrido clásico de mariposa', N'Barrido de cadera / Hip Bump', N'Barrido de tijera',
                     N'Recuperación de guardia desde media guardia', N'Triángulo clásico desde guardia cerrada',
                     N'Pase de media guardia con pierna recta', N'Pase con corte de rodilla / Knee Cut',
                     N'Pase toreando', N'Pase doble por debajo',
                     N'Apertura clásica de guardia cerrada de rodillas',
                     N'Llave recta de tobillo', N'Armbar desde montada',
                     N'Americana desde montada', N'Kimura desde control lateral')
    ORDER BY b.DisplayOrder, t.Name;

    /* 5.3 Debe devolver 0 filas: técnicas con más de un video */
    SELECT t.Name AS TecnicaConMultiplesVideos, COUNT(*) AS Videos
    FROM dbo.Techniques t
    JOIN dbo.TechniqueMedia tm ON tm.TechniqueId = t.Id
    GROUP BY t.Name
    HAVING COUNT(*) > 1;

    /* 5.4 Debe devolver 0 filas: videos compartidos entre técnicas */
    SELECT ma.ExternalVideoId, COUNT(DISTINCT tm.TechniqueId) AS Tecnicas
    FROM dbo.MediaAssets ma
    JOIN dbo.TechniqueMedia tm ON tm.MediaAssetId = ma.Id
    WHERE ma.IsDeleted = 0
    GROUP BY ma.ExternalVideoId
    HAVING COUNT(DISTINCT tm.TechniqueId) > 1;

    /* 5.5 Todos los recursos importados pertenecen al administrador real */
    SELECT COUNT(*) AS RecursosVinculados,
           SUM(CASE WHEN ma.UploadedByUserId = @AdminUserId THEN 1 ELSE 0 END) AS DelAdministrador,
           SUM(CASE WHEN ma.UploadedByUserId <> @AdminUserId THEN 1 ELSE 0 END) AS DeOtroUsuario
    FROM dbo.MediaAssets ma
    JOIN dbo.TechniqueMedia tm ON tm.MediaAssetId = ma.Id;

    /* 5.6 Técnicas en estado administrativo (sin video) */
    SELECT t.Id, t.Name AS TecnicaAdministrativa, t.IsActive, b.Name AS Cinturon
    FROM dbo.Techniques t
    JOIN dbo.Belts b ON b.Id = t.BeltId
    WHERE NOT EXISTS (SELECT 1 FROM dbo.TechniqueMedia tm
                      JOIN dbo.MediaAssets ma ON ma.Id = tm.MediaAssetId AND ma.IsDeleted = 0
                      WHERE tm.TechniqueId = t.Id)
    ORDER BY b.DisplayOrder, t.Name;

    /* 5.7 Auditoría de esta importación */
    SELECT COUNT(*) AS AuditoriasImportacion
    FROM dbo.AuditLogs
    WHERE Details LIKE N'%"Actor":"' + @Actor + N'"%';

    /* 5.8 Alias de las técnicas renombradas */
    SELECT t.Name AS Tecnica, a.Name AS Alias
    FROM dbo.TechniqueAliases a
    JOIN dbo.Techniques t ON t.Id = a.TechniqueId
    WHERE t.Name IN (N'Escape de montada con codos', N'Armbar clásico desde guardia cerrada',
                     N'Pase con corte de rodilla / Knee Cut', N'Triángulo clásico desde guardia cerrada')
    ORDER BY t.Name, a.Name;

    /* 5.9 Posiciones finales */
    SELECT p.Id, p.Name, p.DisplayOrder
    FROM dbo.Positions p
    ORDER BY p.DisplayOrder;
END

DROP TABLE #Catalogo;
DROP TABLE #SinVideoPublicacion;
