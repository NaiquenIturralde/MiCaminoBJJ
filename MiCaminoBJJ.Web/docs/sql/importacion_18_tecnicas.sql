/* =====================================================================================
   Mi Camino BJJ Web — Importación de las 18 técnicas de TecnicasYLinks.pdf
   -------------------------------------------------------------------------------------
   ESTADO: PREPARADO PARA REVISIÓN.  *** NO EJECUTAR SIN AUTORIZACIÓN EXPLÍCITA ***

   Garantías del script
   - Idempotente: puede re-ejecutarse sin duplicar filas (resuelve por NameNormalized,
     ExternalVideoId y por el par TechniqueId/MediaAssetId, que tiene índice único).
   - Transacción ÚNICA (XACT_ABORT ON + TRY/CATCH): o se aplica todo, o nada.
   - Auditoría: una fila en AuditLogs por cada alta/modificación, en la MISMA transacción.
   - Sin DELETE, sin TRUNCATE, sin DROP, sin EnsureDeleted, sin migraciones.
   - No toca Identity ni el usuario/roles: solo usa el Users.Id del administrador.
   - No toca MAUI ni SQLite.
   - Los 18 enlaces del PDF se conservan literalmente (columna VideoUrl).

   Uso previsto (solo cuando se autorice):
     1) BACKUP DATABASE (paso manual, ver PLAN sección 6).
     2) sqlcmd -S "(localdb)\MSSQLLocalDB" -d MiCaminoBJJ_Web_TFI_2026 -E -i importacion_18_tecnicas.sql

   DECISIÓN BLOQUEANTE B1 (ver PLAN):
     @OcultarSinVideo gobierna D3 ("ocultas de la biblioteca pública, visibles en admin"):
       0 = NO ocultar nada (por defecto, conservador).
       1 = ocultar solo las técnicas sin video que NO son de cinturón Blanco.
           Mantiene poblada la sección "Técnicas de la biblioteca para este cinturón"
           del recorrido blanco (Id 3, 4 y 5 siguen visibles).
       2 = ocultar TODAS las técnicas sin video (D3 literal).
           ATENCIÓN: deja VACÍA la sección de biblioteca del recorrido blanco,
           porque sus 3 técnicas de blanco quedarían ocultas.
   ===================================================================================== */

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;   -- obligatorio para índices filtrados / objetos Identity
SET ANSI_NULLS ON;
SET ARITHABORT ON;
SET XACT_ABORT ON;

/* ── Parámetros ─────────────────────────────────────────────────────────────────── */
DECLARE @Actor           nvarchar(100) = N'importacion-18-tecnicas';
DECLARE @AdminUserId     int           = 339;   -- admin@micaminobjj.com (Users.Id, Role=2) VERIFICADO
DECLARE @OcultarSinVideo tinyint       = 0;     -- D3: 0 = no ocultar | 1 = ocultar solo no-Blanco | 2 = ocultar todas
DECLARE @Ahora           datetime2     = SYSUTCDATETIME();

IF @OcultarSinVideo NOT IN (0, 1, 2)
    THROW 50000, N'@OcultarSinVideo debe ser 0, 1 o 2.', 1;

/* ── Guardas de entorno ─────────────────────────────────────────────────────────── */
IF DB_NAME() <> N'MiCaminoBJJ_Web_TFI_2026'
    THROW 50001, N'Base inesperada: este script solo corre sobre MiCaminoBJJ_Web_TFI_2026.', 1;

IF NOT EXISTS (SELECT 1 FROM Users WHERE Id = @AdminUserId AND Role = 2)
    THROW 50002, N'El usuario administrador indicado no existe o no tiene Role = 2.', 1;

/* Estado de partida: pre-importación (11) o post-importación (25, re-ejecución). */
IF (SELECT COUNT(*) FROM Techniques) NOT IN (11, 25)
    THROW 50003, N'El catálogo no está en el estado esperado (11 antes / 25 después). Abortado.', 1;

IF (SELECT COUNT(*) FROM Techniques WHERE Id IN (1, 2, 8, 9)) <> 4
    THROW 50004, N'No existen las 4 técnicas existentes a reutilizar (Ids 1, 2, 8, 9).', 1;

/* Ids 1,2,8,9 deben tener el nombre heredado o el del PDF (permite re-ejecución).
   Cualquier otro nombre significa que la fila fue tocada por fuera de este proceso. */
IF EXISTS (
    SELECT 1 FROM Techniques
    WHERE Id IN (1,2,8,9)
      AND ISNULL(NameNormalized, N'') NOT IN (
            N'ARMBAR DESDE GUARDIA CERRADA', N'ESCAPE DE MONTADA',
            N'PASS DE CORTE DE RODILLA',     N'TRIANGULO DESDE GUARDIA CERRADA',
            N'ARMBAR CLASICO DESDE GUARDIA CERRADA', N'ESCAPE DE MONTADA CON CODOS',
            N'PASE CON CORTE DE RODILLA',    N'TRIANGULO CLASICO DESDE GUARDIA CERRADA')
)
    THROW 50006, N'Alguna de las técnicas 1/2/8/9 fue renombrada por fuera de este proceso. Revisar antes de continuar.', 1;

/* ── Mapa único de verdad: las 18 filas del PDF ──────────────────────────────────
   NombreNorm / AliasNorm replican EXACTAMENTE TextNormalizer.Normalize
   (FormD sin diacríticos + mayúsculas + espacios colapsados). Se validan abajo.
   ExistenteId = Id a actualizar; NULL = técnica nueva.
   ───────────────────────────────────────────────────────────────────────────────── */
DECLARE @Imp TABLE
(
    Orden       int            NOT NULL PRIMARY KEY,
    Nombre      nvarchar(200)  NOT NULL,
    NombreNorm  nvarchar(200)  NOT NULL,
    Alias       nvarchar(200)  NULL,
    AliasNorm   nvarchar(200)  NULL,
    Cinturon    nvarchar(50)   NOT NULL,
    Categoria   nvarchar(50)   NOT NULL,
    Posicion    nvarchar(50)   NOT NULL,
    ExistenteId int            NULL,
    VideoId     char(11)       NOT NULL,
    VideoUrl    nvarchar(200)  NOT NULL,
    VideoTitulo nvarchar(300)  NOT NULL,
    Descripcion nvarchar(2000) NOT NULL,
    Objetivo    nvarchar(1000) NOT NULL
);

INSERT @Imp (Orden, Nombre, NombreNorm, Alias, AliasNorm, Cinturon, Categoria, Posicion, ExistenteId,
             VideoId, VideoUrl, VideoTitulo, Descripcion, Objetivo)
VALUES
/* ── AZUL (3) ──────────────────────────────────────────────────────────────────── */
( 1, N'Escape de montada con codos', N'ESCAPE DE MONTADA CON CODOS',
     N'Escape de montada', N'ESCAPE DE MONTADA',
     N'Azul', N'Escape', N'Montada', 2,
  'ynSK1oYc_gg', N'https://youtube.com/shorts/ynSK1oYc_gg',
  N'Escape de montada con codos | Brazilian Jiu-Jitsu',
  N'Escape de montada que utiliza los codos y el movimiento de cadera para generar espacio y recuperar una posición defensiva.',
  N'Reconocer una posición desfavorable, proteger brazos y cuello, usar la estructura corporal y el movimiento de cadera, y salir de la montada de forma controlada.'),

( 2, N'Escape de armbar hacia pase de guardia', N'ESCAPE DE ARMBAR HACIA PASE DE GUARDIA',
     NULL, NULL,
     N'Azul', N'Escape', N'Guardia cerrada', NULL,
  'yLGmZxSf2Vo', N'https://youtu.be/yLGmZxSf2Vo',
  N'Escape de Armbar hacia Pase de Guardia | Brazilian Jiu-Jitsu',
  N'Defensa ante un intento de armbar que busca evitar la finalización y transformar la situación defensiva en una oportunidad para avanzar hacia el pase de guardia.',
  N'Reconocer la amenaza de llave de brazo, proteger el brazo, controlar la postura y continuar hacia una posición de mayor control.'),

( 3, N'Escape de control lateral y recuperación de guardia', N'ESCAPE DE CONTROL LATERAL Y RECUPERACION DE GUARDIA',
     NULL, NULL,
     N'Azul', N'Escape', N'Control lateral', NULL,
  'eDUjKxV6wb0', N'https://youtu.be/eDUjKxV6wb0',
  N'Escape de Control Lateral y Recuperación de Guardia | Brazilian Jiu-Jitsu',
  N'Salida desde control lateral que utiliza la estructura defensiva, la movilidad de cadera y la creación de espacio para recuperar la guardia.',
  N'Reconocer el control lateral, proteger cuello y brazos, generar espacio con la cadera y recuperar la guardia mejorando el control de la distancia.'),

/* ── VIOLETA (6) ───────────────────────────────────────────────────────────────── */
( 4, N'Armbar clásico desde guardia cerrada', N'ARMBAR CLASICO DESDE GUARDIA CERRADA',
     N'Armbar desde guardia cerrada', N'ARMBAR DESDE GUARDIA CERRADA',
     N'Violeta', N'Sumisión', N'Guardia cerrada', 1,
  'QY19oOtQPnw', N'https://youtu.be/QY19oOtQPnw',
  N'Armbar Clásico desde Guardia Cerrada | Brazilian Jiu-Jitsu',
  N'Armbar clásico desde guardia cerrada que combina el control del brazo, el movimiento de cadera y el posicionamiento corporal para aplicar una llave sobre el codo.',
  N'Reconocer la guardia cerrada como posición de ataque, controlar el brazo y crear ángulos con la cadera respetando la seguridad de las llaves articulares.'),

( 5, N'Barrido clásico de mariposa', N'BARRIDO CLASICO DE MARIPOSA',
     NULL, NULL,
     N'Violeta', N'Guardia', N'Guardia mariposa', NULL,
  'D_dVjIm-hlI', N'https://youtu.be/D_dVjIm-hlI',
  N'Barrido Clásico de Mariposa | Brazilian Jiu-Jitsu',
  N'Barrido clásico desde guardia mariposa que utiliza el control del equilibrio, los ganchos de las piernas y el movimiento corporal para invertir la posición del oponente.',
  N'Comprender la función de los ganchos, aprovechar el desequilibrio del oponente y coordinar el movimiento del cuerpo para invertir la posición.'),

( 6, N'Barrido de cadera', N'BARRIDO DE CADERA',
     NULL, NULL,
     N'Violeta', N'Guardia', N'Guardia cerrada', NULL,
  'jsB4Iv8vgAM', N'https://youtu.be/jsB4Iv8vgAM',
  N'Barrido de Cadera | Hip Bump Sweep — Brazilian Jiu-Jitsu',
  N'Barrido de cadera desde guardia cerrada que aprovecha el movimiento corporal y el desequilibrio del oponente para invertir la posición y obtener control superior.',
  N'Reconocer cuándo usar el barrido, elevar el torso, movilizar la cadera y aprovechar el equilibrio del oponente para pasar a una posición superior.'),

( 7, N'Barrido de tijera', N'BARRIDO DE TIJERA',
     NULL, NULL,
     N'Violeta', N'Guardia', N'Guardia cerrada', NULL,
  'O3KBgF9umwQ', N'https://youtu.be/O3KBgF9umwQ',
  N'Barrido de Tijera | Scissor Sweep — Brazilian Jiu-Jitsu',
  N'Barrido de tijera que combina el control de la parte superior del cuerpo, el posicionamiento de las piernas y el desequilibrio para conseguir una inversión de posición.',
  N'Controlar la postura, coordinar las piernas con el movimiento corporal y finalizar en una posición superior estable.'),

( 8, N'Recuperación de guardia desde media guardia', N'RECUPERACION DE GUARDIA DESDE MEDIA GUARDIA',
     NULL, NULL,
     N'Violeta', N'Guardia', N'Media guardia', NULL,
  'sCmv4Jc0qlU', N'https://youtu.be/sCmv4Jc0qlU',
  N'Recuperación de Guardia desde Media Guardia | Brazilian Jiu-Jitsu',
  N'Recuperación de guardia desde media guardia que utiliza la estructura defensiva, el movimiento de cadera y el control de la distancia para mejorar la posición.',
  N'Reconocer la media guardia y sus posibilidades defensivas, mantener una estructura segura, generar espacio con la cadera y acceder a una guardia más estable.'),

( 9, N'Triángulo clásico desde guardia cerrada', N'TRIANGULO CLASICO DESDE GUARDIA CERRADA',
     N'Triángulo desde guardia cerrada', N'TRIANGULO DESDE GUARDIA CERRADA',
     N'Violeta', N'Sumisión', N'Guardia cerrada', 9,
  '7A72vo39F3Q', N'https://youtu.be/7A72vo39F3Q',
  N'Triángulo Clásico desde Guardia Cerrada | Brazilian Jiu-Jitsu',
  N'Triángulo clásico desde guardia cerrada que utiliza las piernas y el posicionamiento corporal para controlar al oponente y aplicar una estrangulación.',
  N'Controlar la postura y los ángulos, comprender el papel de las piernas y aplicar las precauciones necesarias en estrangulaciones.'),

/* ── MARRÓN (5) ────────────────────────────────────────────────────────────────── */
(10, N'Pase de media guardia con pierna recta', N'PASE DE MEDIA GUARDIA CON PIERNA RECTA',
     NULL, NULL,
     N'Marrón', N'Control', N'Media guardia', NULL,
  'ZEIcluOsrL8', N'https://youtu.be/ZEIcluOsrL8',
  N'Pase de Media Guardia con Pierna Recta | Brazilian Jiu-Jitsu',
  N'Pase de media guardia que utiliza el posicionamiento corporal, el control del oponente y el movimiento de la pierna para superar la defensa y avanzar a una posición de mayor control.',
  N'Mantener equilibrio y presión controlada, liberar la pierna atrapada y transicionar a una posición de control superior.'),

(11, N'Pase con corte de rodilla', N'PASE CON CORTE DE RODILLA',
     N'Pass de corte de rodilla', N'PASS DE CORTE DE RODILLA',
     N'Marrón', N'Control', N'Control lateral', 8,
  '7fFBZ7DgKs4', N'https://youtu.be/7fFBZ7DgKs4',
  N'Pase con Corte de Rodilla | Knee Cut Pass — Brazilian Jiu-Jitsu',
  N'Pase que supera la guardia mediante el control de la parte superior del cuerpo, el posicionamiento de la cadera y el desplazamiento de la rodilla.',
  N'Controlar la distancia y la postura, usar la cadera y la rodilla durante el pase y llegar al control lateral.'),

(12, N'Pase toreando', N'PASE TOREANDO',
     NULL, NULL,
     N'Marrón', N'Control', N'Guardia abierta', NULL,
  'ZGStoRHimbE', N'https://youtu.be/ZGStoRHimbE',
  N'Pase Toreando | Brazilian Jiu-Jitsu',
  N'Pase toreando que supera la guardia abierta mediante el control de las piernas, el desplazamiento lateral y la coordinación corporal.',
  N'Reconocer la oportunidad de pasar una guardia abierta, controlar las piernas y usar el desplazamiento lateral para llegar a una posición dominante.'),

(13, N'Pase doble por debajo', N'PASE DOBLE POR DEBAJO',
     NULL, NULL,
     N'Marrón', N'Control', N'Guardia abierta', NULL,
  'zrUofVX5Xgk', N'https://youtu.be/zrUofVX5Xgk',
  N'Pase Doble por Debajo | Double Under Pass — Brazilian Jiu-Jitsu',
  N'Pase doble por debajo que controla ambas piernas y usa el posicionamiento corporal para limitar la movilidad del oponente y avanzar hacia una posición de control.',
  N'Controlar las piernas, mantener una postura estable durante el avance y llegar a una posición superior segura.'),

(14, N'Apertura clásica de guardia cerrada de rodillas', N'APERTURA CLASICA DE GUARDIA CERRADA DE RODILLAS',
     NULL, NULL,
     N'Marrón', N'Control', N'Guardia cerrada', NULL,
  'GK7g0oSZWvU', N'https://youtube.com/shorts/GK7g0oSZWvU',
  N'Apertura Clásica de Guardia Cerrada de Rodillas | Brazilian Jiu-Jitsu',
  N'Apertura tradicional de guardia cerrada desde una posición arrodillada, usando el control de la postura, una base estable y el posicionamiento corporal para abrir la guardia.',
  N'Conservar una postura estable, generar espacio para abrir la guardia e identificar la transición hacia una oportunidad de pase.'),

/* ── NEGRO (4) ─────────────────────────────────────────────────────────────────── */
(15, N'Llave recta de tobillo', N'LLAVE RECTA DE TOBILLO',
     NULL, NULL,
     N'Negro', N'Sumisión', N'Control de piernas', NULL,
  '9KvpsjNfHNk', N'https://youtu.be/9KvpsjNfHNk',
  N'Llave Recta de Tobillo | Straight Ankle Lock — Brazilian Jiu-Jitsu',
  N'Llave recta de tobillo que utiliza el control de la pierna y una presión controlada sobre la articulación del tobillo.',
  N'Controlar la pierna, comprender la mecánica corporal y aplicar las precauciones necesarias en las sumisiones de pierna.'),

(16, N'Armbar desde montada', N'ARMBAR DESDE MONTADA',
     NULL, NULL,
     N'Negro', N'Sumisión', N'Montada', NULL,
  'VToH_xYHFk0', N'https://youtu.be/VToH_xYHFk0',
  N'Armbar desde Montada | Brazilian Jiu-Jitsu',
  N'Armbar desde montada que busca controlar el brazo del oponente y aplicar una llave sobre la articulación del codo.',
  N'Reconocer la montada y sus posibilidades de ataque, controlar el brazo y el equilibrio corporal y aplicar los cuidados de ejecución.'),

(17, N'Americana desde montada', N'AMERICANA DESDE MONTADA',
     NULL, NULL,
     N'Negro', N'Sumisión', N'Montada', NULL,
  'NcyJYHf64wg', N'https://youtu.be/NcyJYHf64wg',
  N'Americana desde Montada | Brazilian Jiu-Jitsu',
  N'Americana desde montada que utiliza el control del brazo y la rotación del hombro para aplicar una llave articular.',
  N'Mantener una base estable, reconocer el control del brazo necesario y usar el posicionamiento corporal para sostener el control.'),

(18, N'Kimura desde control lateral', N'KIMURA DESDE CONTROL LATERAL',
     NULL, NULL,
     N'Negro', N'Sumisión', N'Control lateral', NULL,
  'AXzy8QA4bv0', N'https://youtu.be/AXzy8QA4bv0',
  N'Kimura desde Control Lateral | Brazilian Jiu-Jitsu',
  N'Kimura desde control lateral que combina el control del brazo con una llave de rotación sobre la articulación del hombro.',
  N'Controlar el brazo del oponente, mantener estabilidad durante la transición hacia la sumisión e identificar los principios de control y seguridad.');

/* ── Validaciones del mapa ──────────────────────────────────────────────────────── */
IF (SELECT COUNT(*) FROM @Imp) <> 18
    THROW 50010, N'El mapa no contiene 18 filas.', 1;

IF EXISTS (SELECT VideoId FROM @Imp GROUP BY VideoId HAVING COUNT(*) > 1)
    THROW 50011, N'Hay identificadores de video repetidos en el mapa.', 1;

IF EXISTS (SELECT NombreNorm FROM @Imp GROUP BY NombreNorm HAVING COUNT(*) > 1)
    THROW 50012, N'Hay nombres de técnica repetidos en el mapa.', 1;

IF EXISTS (
    SELECT 1 FROM @Imp i
    WHERE NOT EXISTS (SELECT 1 FROM Belts b WHERE b.Name = i.Cinturon)
       OR NOT EXISTS (SELECT 1 FROM TechniqueCategories c WHERE c.Name = i.Categoria)
)
    THROW 50013, N'Algún cinturón o categoría del mapa no existe en el catálogo.', 1;

IF EXISTS (
    SELECT 1 FROM @Imp i
    WHERE i.ExistenteId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM Techniques t WHERE t.Id = i.ExistenteId)
)
    THROW 50014, N'Alguna técnica marcada como existente no está en la base.', 1;

/* NombreNorm se escribió a mano con la regla de TextNormalizer; se revalida contra
   una normalización independiente para no persistir un valor inconsistente. */
IF EXISTS (
    SELECT 1 FROM @Imp
    WHERE NombreNorm COLLATE Latin1_General_BIN2
          <> UPPER(LTRIM(RTRIM(Nombre))) COLLATE Latin1_General_BIN2
)
    THROW 50015, N'NombreNorm no coincide con el nombre visible normalizado.', 1;

/* ── Capturas deterministas ─────────────────────────────────────────────────────── */
DECLARE @Auditorias TABLE (Id int NOT NULL);
DECLARE @PosCreadas  TABLE (PositionId int NOT NULL);
DECLARE @Actualizadas TABLE (TechniqueId int NOT NULL);
DECLARE @Nuevas       TABLE (TechniqueId int NOT NULL);
DECLARE @Assets       TABLE (MediaAssetId int NOT NULL);
DECLARE @Vinculos     TABLE (TechniqueMediaId int NOT NULL);
DECLARE @Ocultadas    TABLE (TechniqueId int NOT NULL);

BEGIN TRY
    BEGIN TRANSACTION;

    /* ══ 1. POSICIONES NUEVAS (3) ═══════════════════════════════════════════════ */
    DECLARE @Pos TABLE (Nombre nvarchar(200) NOT NULL PRIMARY KEY, Orden int NOT NULL);
    INSERT @Pos VALUES (N'Guardia abierta', 6), (N'Guardia mariposa', 7), (N'Control de piernas', 8);

    INSERT Positions (Name, DisplayOrder, CreatedAt)
    OUTPUT inserted.Id INTO @PosCreadas
    SELECT p.Nombre, p.Orden, @Ahora
    FROM @Pos p
    WHERE NOT EXISTS (SELECT 1 FROM Positions x WHERE x.Name = p.Nombre);

    INSERT AuditLogs (UserId, Action, EntityType, EntityId, TimestampUtc, Result, Details)
    OUTPUT inserted.Id INTO @Auditorias
    SELECT @AdminUserId, 0 /* Create */, N'Position', pc.PositionId, @Ahora, 0,
           (SELECT @Actor AS Actor, x.Name AS Name, x.DisplayOrder AS DisplayOrder,
                   N'importacion-18-tecnicas' AS Operacion
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
    FROM @PosCreadas pc
    INNER JOIN Positions x ON x.Id = pc.PositionId;

    /* ══ 2. TÉCNICAS EXISTENTES (4): título del PDF + cinturón + alias (D5) ════ */
    INSERT @Actualizadas (TechniqueId)
    SELECT t.Id
    FROM Techniques t
    INNER JOIN @Imp i ON i.ExistenteId = t.Id
    INNER JOIN Belts b ON b.Name = i.Cinturon
    WHERE ISNULL(t.NameNormalized, N'') <> i.NombreNorm
       OR t.BeltId <> b.Id
       OR t.Name <> i.Nombre;

    UPDATE t
       SET t.Name           = i.Nombre,
           t.NameNormalized = i.NombreNorm,
           t.BeltId         = b.Id,
           t.UpdatedAt      = @Ahora
    FROM Techniques t
    INNER JOIN @Imp i         ON i.ExistenteId = t.Id
    INNER JOIN Belts b        ON b.Name = i.Cinturon
    INNER JOIN @Actualizadas a ON a.TechniqueId = t.Id;

    INSERT AuditLogs (UserId, Action, EntityType, EntityId, TimestampUtc, Result, Details)
    OUTPUT inserted.Id INTO @Auditorias
    SELECT @AdminUserId, 1 /* Update */, N'Technique', t.Id, @Ahora, 0,
           (SELECT @Actor AS Actor, t.Name AS NewName, t.NameNormalized AS NewNameNormalized,
                   b.Name AS NewBelt, N'importacion-18-tecnicas' AS Operacion
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
    FROM Techniques t
    INNER JOIN Belts b         ON b.Id = t.BeltId
    INNER JOIN @Actualizadas a ON a.TechniqueId = t.Id;

    /* Alias con el nombre heredado. El índice único (TechniqueId, NameNormalized)
       evita duplicados; se filtra además para no repetir un alias ya existente. */
    INSERT TechniqueAliases (TechniqueId, Name, NameNormalized, CreatedAt)
    SELECT i.ExistenteId, i.Alias, i.AliasNorm, @Ahora
    FROM @Imp i
    WHERE i.Alias IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM TechniqueAliases a
                      WHERE a.TechniqueId = i.ExistenteId AND a.NameNormalized = i.AliasNorm)
      AND NOT EXISTS (SELECT 1 FROM Techniques t
                      WHERE t.Id = i.ExistenteId AND ISNULL(t.NameNormalized, N'') = i.AliasNorm);

    /* ══ 3. TÉCNICAS NUEVAS (14) ═══════════════════════════════════════════════ */
    INSERT Techniques
        (Name, NameNormalized, Description, Objective, Steps, Tips,
         BeltId, CategoryId, PositionId, IsActive, CreatedAt, UpdatedAt)
    OUTPUT inserted.Id INTO @Nuevas
    SELECT i.Nombre, i.NombreNorm, i.Descripcion, i.Objetivo, NULL, NULL,
           b.Id, c.Id, p.Id, 1, @Ahora, NULL
    FROM @Imp i
    INNER JOIN Belts b                ON b.Name = i.Cinturon
    INNER JOIN TechniqueCategories c  ON c.Name = i.Categoria
    INNER JOIN Positions p            ON p.Name = i.Posicion
    WHERE i.ExistenteId IS NULL
      AND NOT EXISTS (SELECT 1 FROM Techniques t WHERE t.NameNormalized = i.NombreNorm);

    INSERT AuditLogs (UserId, Action, EntityType, EntityId, TimestampUtc, Result, Details)
    OUTPUT inserted.Id INTO @Auditorias
    SELECT @AdminUserId, 0, N'Technique', n.TechniqueId, @Ahora, 0,
           (SELECT @Actor AS Actor, t.Name AS Name, b.Name AS Belt,
                   c.Name AS Category, p.Name AS Position,
                   N'importacion-18-tecnicas' AS Operacion
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
    FROM @Nuevas n
    INNER JOIN Techniques t          ON t.Id = n.TechniqueId
    INNER JOIN Belts b               ON b.Id = t.BeltId
    INNER JOIN TechniqueCategories c ON c.Id = t.CategoryId
    INNER JOIN Positions p           ON p.Id = t.PositionId;

    /* ══ 4. RECURSOS MULTIMEDIA (18) ═══════════════════════════════════════════ */
    INSERT MediaAssets
        (UploadedByUserId, MediaType, SourceType, ExternalUrl, ExternalProvider,
         ExternalVideoId, Title, SyncStatus, CreatedAt, IsDeleted)
    OUTPUT inserted.Id INTO @Assets
    SELECT @AdminUserId,
           1,  /* MediaType.Video */
           3,  /* MediaSourceType.ExternalLink */
           i.VideoUrl,
           1,  /* ExternalMediaProvider.YouTube */
           i.VideoId,
           i.VideoTitulo,
           0,  /* SyncStatus.LocalOnly */
           @Ahora,
           0
    FROM @Imp i
    WHERE NOT EXISTS (SELECT 1 FROM MediaAssets m
                      WHERE m.ExternalVideoId = i.VideoId AND m.IsDeleted = 0);

    INSERT AuditLogs (UserId, Action, EntityType, EntityId, TimestampUtc, Result, Details)
    OUTPUT inserted.Id INTO @Auditorias
    SELECT @AdminUserId, 0, N'MediaAsset', a.MediaAssetId, @Ahora, 0,
           (SELECT @Actor AS Actor, m.ExternalVideoId AS ExternalVideoId,
                   m.ExternalUrl AS ExternalUrl, N'YouTube' AS Provider,
                   N'importacion-18-tecnicas' AS Operacion
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
    FROM @Assets a
    INNER JOIN MediaAssets m ON m.Id = a.MediaAssetId;

    /* ══ 5. VÍNCULOS TÉCNICA ↔ VIDEO (18) ═════════════════════════════════════ */
    INSERT TechniqueMedia (TechniqueId, MediaAssetId, IsCover, DisplayOrder, CreatedAt)
    OUTPUT inserted.Id INTO @Vinculos
    SELECT t.Id, m.Id, 0, 1, @Ahora
    FROM @Imp i
    INNER JOIN Techniques  t ON t.NameNormalized = i.NombreNorm
    INNER JOIN MediaAssets m ON m.ExternalVideoId = i.VideoId AND m.IsDeleted = 0
    WHERE NOT EXISTS (SELECT 1 FROM TechniqueMedia tm
                      WHERE tm.TechniqueId = t.Id AND tm.MediaAssetId = m.Id);

    INSERT AuditLogs (UserId, Action, EntityType, EntityId, TimestampUtc, Result, Details)
    OUTPUT inserted.Id INTO @Auditorias
    SELECT @AdminUserId, 0, N'TechniqueMedia', v.TechniqueMediaId, @Ahora, 0,
           (SELECT @Actor AS Actor, tm.TechniqueId AS TechniqueId,
                   tm.MediaAssetId AS MediaAssetId, tm.DisplayOrder AS DisplayOrder,
                   N'importacion-18-tecnicas' AS Operacion
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
    FROM @Vinculos v
    INNER JOIN TechniqueMedia tm ON tm.Id = v.TechniqueMediaId;

    /* ══ 6. D3 — Ocultar de la biblioteca pública las técnicas sin video ═══════ */
    IF @OcultarSinVideo > 0
    BEGIN
        INSERT @Ocultadas (TechniqueId)
        SELECT t.Id
        FROM Techniques t
        INNER JOIN Belts b ON b.Id = t.BeltId
        WHERE t.IsActive = 1
          AND NOT EXISTS (SELECT 1 FROM TechniqueMedia tm WHERE tm.TechniqueId = t.Id)
          /* modo 1: nunca ocultar las de cinturón Blanco (sostienen el recorrido). */
          AND (@OcultarSinVideo = 2 OR b.Name <> N'Blanco');

        UPDATE t
           SET t.IsActive = 0, t.UpdatedAt = @Ahora
        FROM Techniques t
        INNER JOIN @Ocultadas o ON o.TechniqueId = t.Id;

        INSERT AuditLogs (UserId, Action, EntityType, EntityId, TimestampUtc, Result, Details)
        OUTPUT inserted.Id INTO @Auditorias
        SELECT @AdminUserId, 2 /* SoftDelete */, N'Technique', o.TechniqueId, @Ahora, 0,
               (SELECT @Actor AS Actor, CAST(0 AS bit) AS IsActive,
                       N'oculto-por-falta-de-video' AS Motivo,
                       N'importacion-18-tecnicas' AS Operacion
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
        FROM @Ocultadas o;
    END

    /* ══ 7. VERIFICACIONES DE CIERRE (si fallan => ROLLBACK) ══════════════════ */
    IF (SELECT COUNT(*) FROM Techniques) <> 25
        THROW 50020, N'Verificación fallida: el catálogo debe quedar con 25 técnicas.', 1;

    IF (SELECT COUNT(DISTINCT tm.TechniqueId) FROM TechniqueMedia tm) <> 18
        THROW 50021, N'Verificación fallida: 18 técnicas deben quedar con video.', 1;

    IF (SELECT COUNT(*) FROM MediaAssets WHERE ExternalProvider = 1 AND IsDeleted = 0) <> 18
        THROW 50022, N'Verificación fallida: 18 recursos de YouTube esperados.', 1;

    IF EXISTS (
        SELECT i.VideoId FROM @Imp i
        WHERE NOT EXISTS (
            SELECT 1 FROM TechniqueMedia tm
            INNER JOIN MediaAssets m ON m.Id = tm.MediaAssetId
            WHERE m.ExternalVideoId = i.VideoId)
    )
        THROW 50023, N'Verificación fallida: algún video del PDF quedó sin vincular.', 1;

    IF EXISTS (
        SELECT i.VideoId FROM @Imp i
        GROUP BY i.VideoId
        HAVING (SELECT COUNT(*) FROM TechniqueMedia tm
                INNER JOIN MediaAssets m ON m.Id = tm.MediaAssetId
                WHERE m.ExternalVideoId = i.VideoId) > 1
    )
        THROW 50024, N'Verificación fallida: un video del PDF quedó vinculado a más de una técnica.', 1;

    IF (SELECT COUNT(*) FROM Positions
        WHERE Name IN (N'Guardia abierta', N'Guardia mariposa', N'Control de piernas')) <> 3
        THROW 50025, N'Verificación fallida: faltan las posiciones nuevas.', 1;

    IF EXISTS (
        SELECT 1 FROM @Imp i
        INNER JOIN Techniques t ON t.NameNormalized = i.NombreNorm
        INNER JOIN Belts b ON b.Id = t.BeltId
        WHERE b.Name <> i.Cinturon
    )
        THROW 50026, N'Verificación fallida: alguna técnica no quedó en el cinturón indicado.', 1;

    IF EXISTS (
        SELECT 1 FROM @Imp i
        INNER JOIN Techniques t ON t.NameNormalized = i.NombreNorm
        INNER JOIN TechniqueCategories c ON c.Id = t.CategoryId
        INNER JOIN Positions p ON p.Id = t.PositionId
        WHERE c.Name <> i.Categoria OR p.Name <> i.Posicion
    )
        THROW 50027, N'Verificación fallida: categoría o posición incorrecta en alguna técnica.', 1;

    COMMIT TRANSACTION;

    /* ══ 8. RESUMEN ════════════════════════════════════════════════════════════ */
    SELECT
        (SELECT COUNT(*) FROM @PosCreadas)                     AS PosicionesNuevas,
        (SELECT COUNT(*) FROM @Actualizadas)                   AS TecnicasActualizadas,
        (SELECT COUNT(*) FROM @Nuevas)                         AS TecnicasNuevas,
        (SELECT COUNT(*) FROM @Assets)                         AS RecursosCreados,
        (SELECT COUNT(*) FROM @Vinculos)                       AS VinculosCreados,
        (SELECT COUNT(*) FROM @Ocultadas)                      AS TecnicasOcultadas,
        (SELECT COUNT(*) FROM @Auditorias)                     AS AuditoriasEscritas,
        (SELECT COUNT(*) FROM Techniques)                      AS TotalTecnicas,
        (SELECT COUNT(*) FROM Techniques WHERE IsActive = 1)   AS TecnicasPublicas,
        (SELECT COUNT(*) FROM TechniqueMedia)                  AS TotalVinculos,
        (SELECT COUNT(*) FROM MediaAssets WHERE IsDeleted = 0) AS TotalRecursos,
        (SELECT COUNT(*) FROM TechniqueAliases)                AS TotalAlias,
        (SELECT COUNT(*) FROM Positions)                       AS TotalPosiciones;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    /* THROW solo admite números >= 50000; se conserva el original en el mensaje. */
    DECLARE @msg nvarchar(2048) = CONCAT(
        N'Importación abortada y REVERTIDA. Error ', ERROR_NUMBER(), N': ', ERROR_MESSAGE());
    THROW 50999, @msg, 1;
END CATCH
