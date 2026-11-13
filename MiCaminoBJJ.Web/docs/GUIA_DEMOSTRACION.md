# Guía de demostración — Mi Camino BJJ Web (TFI)

> **Duración total: 8–10 minutos** · Presentación: miércoles 4 de noviembre de 2026.
> Recorrido pensado para el navegador en escritorio (1440×900). Todo lo marcado como ✔ es funcional y verificado con pruebas; lo marcado como ⛔ es **pendiente: no presentarlo como implementado**.

## Antes de empezar (checklist, 2 minutos)

1. `dotnet run --project MiCaminoBJJ.Web` → app en `http://localhost:5097`.
2. Verificar `http://localhost:5097/` responde.
3. Tener a mano la credencial de Development: `dotnet user-secrets list --project MiCaminoBJJ.Web` (clave `AdminDev:Password`). **Nunca mostrarla en pantalla proyectada** (mostrar solo el comando, no el valor).
4. Si se quiere mostrar SQL Server: tener `sqlcmd` o SSMS disponible (opcional, paso 4).

---

## 1. Objetivo del proyecto (~45 s)

- **Mostrar**: portada sin interactuar todavía.
- **Decir**: "Mi Camino BJJ es una plataforma educativa de Brazilian Jiu-Jitsu: biblioteca de técnicas con búsqueda por sinónimos, registro de entrenamientos y seguimiento de progreso. Hoy presento la **versión web** sobre la misma arquitectura compartida que la app móvil."
- **Tecnología**: solución .NET 10 multiproyecto (Core / Infrastructure / Web / MAUI).
- ⛔ No mencionar sincronización, Docker, IA o tiendas como cosas hechas.

## 2. Página principal (~45 s)

- **Mostrar**: `/` — hero negro y dorado, secciones "Funcionalidades" y "Una arquitectura, dos experiencias"; probar el botón "Ver funcionalidades" (ancla).
- **Decir**: "La identidad visual —fondo #171717, dorado #D7B83E— es común a toda la solución y está aprobada en las tres resoluciones."
- **Tecnología**: Razor Views + Bootstrap 5, CSS propio sin framework extra.

## 3. Biblioteca y búsquedas (~1 min 30 s)

- **Mostrar** en `/Tecnicas`, en este orden:
  1. Listado completo (11 técnicas).
  2. Buscar `Kimura` → técnicas con "Kimura" en el nombre.
  3. Buscar `RNC` → aparece **Mata león** (alias: la buscó por sinónimo, no por nombre).
  4. Buscar `mata LEON` (sin tilde ni ñ, caja mezclada) → encuentra "Mata león".
  5. Filtros combinados: cinturón **Azul** + categoría **Sumisión** → 2 resultados.
- **Decir**: "La normalización se aplica en las dos orillas: el término se normaliza en el servidor con la misma regla que usa la app móvil; la búsqueda toca columnas `NameNormalized` de técnicas y alias."
- **Tecnología**: EF Core traducido a SQL Server + `TextNormalizer` compartido con Core.
- ⛔ La biblioteca NO permite crear técnicas desde la vista pública (eso es del panel admin).

## 4. Detalle y vuelta con filtros (~1 min)

- **Mostrar**: abrir "Kimura desde guardia cerrada" → descripción, objetivo, pasos, consejos y "También conocida como" (alias). Clic en **"Volver con los filtros aplicados"** → el listado vuelve con los selects cargados.
- **Decir**: "El detalle conserva el contexto: los filtros viajan en la URL y el modelo los rehidrata. Un id inexistente responde HTTP 404, probado por pruebas."
- **Tecnología**: ViewModel de detalle con parseo de JSON (pasos/consejos), routing y estado sin sesión del lado del servidor.

## 5. SQL Server (~1 min)

- **Mostrar** (elegir UNA opción):
  - **(a) Terminal**: `sqlcmd -S "(localdb)\MSSQLLocalDB" -E -d MiCaminoBJJ_Web_TFI_2026` y una consulta corta:
    `SELECT COUNT(*) FROM Techniques; SELECT name FROM sys.tables;`
  - **(b) SSMS/VS Code**: expandir la base `MiCaminoBJJ_Web_TFI_2026` → 21 tablas → abrir `Techniques` y `AuditLogs`.
- **Decir**: "La web usa una base **exclusiva** `MiCaminoBJJ_Web_TFI_2026` en LocalDB, con su propia migración (`20261030150827_InitialSqlServer`), separada de la SQLite de la app móvil. 11 técnicas y 13 alias cargados por un seeder idempotente. La app **nunca** ejecuta EnsureCreated ni migraciones automáticas."
- **Tecnología**: EF Core 10 + proveedor SQL Server, migraciones por ensamblado separado.
- ⛔ No tocar ni mostrar bases de otros proyectos (`CryptoViewDb`, `SistemaEducativoDb`, etc.): son ajenas al TFI.

## 6. Acceso administrativo local (~1 min)

- **Mostrar**:
  1. Abrir en ventana anónima/otro navegador `http://localhost:5097/AdminTecnicas` → **redirige a login**.
  2. En la ventana de demo: ir a `/AdminAcceso/Login` e ingresar la credencial de Development (mostrar solo el campo, no el valor).
- **Decir**: "Sin Identity todavía: existe **una credencial de desarrollo** guardada fuera del repositorio (User Secrets), validada en el servidor con comparación de tiempo fijo. La cookie es HttpOnly, SameSite, no persistente. **Fuera de Development todo el módulo responde 404** (verificado en un arranque con `ASPNETCORE_ENVIRONMENT=Production`)."
- **Tecnología**: autenticación por cookie del framework, Data Protection, cabeceras `X-Frame-Options: DENY` y antiforgery.
- ⛔ No presentar el módulo como accesible en producción ni como multiusuario.

## 7. CRUD: alta (~1 min 30 s)

- **Mostrar**: "Nueva técnica" → completar nombre/descripción/objetivo/pasos/consejos + cinturón/categoría/posición → "Crear técnica" → mensaje de éxito y la fila en el listado.
- **Variante rápida (30 s)**: enviar el formulario **vacío** → mensajes de validación en rojo (validación del servidor).
- **Decir**: "El ViewModel nunca recibe la entidad EF: DataAnnotations con los límites del dominio (nombre ≤ 200) y, además, el servicio valida duplicados por nombre normalizado —probaría escribir 'Kimura desde guardia cerrada' si hay tiempo— y la existencia de las claves foráneas."
- **Tecnología**: MVC model binding + validación en dos capas + antiforgery en el POST.

## 8. Efecto en la biblioteca, edición y estados (~2 min)

- **Mostrar** (con la técnica recién creada):
  1. `/Tecnicas?buscar=<texto>` → **ya aparece en la biblioteca pública** (misma base).
  2. Editar → cambiar la descripción → "Guardar cambios" → volver a la pública → texto actualizado.
  3. "Desactivar" → **modal de confirmación** → aceptar → mensaje; buscarla en la pública → **desaparece** ("Sin resultados").
  4. Filtro Estado = "Desactivadas" → aparece con badge "Desactivada" → "Reactivar" → **vuelve** a la pública.
- **Decir**: "Desactivar es una baja **lógica**: no hay eliminación física en la web. El estado se refleja en la pública porque ambas lecturas usan la misma base."
- **Tecnología**: `IsActive` como bandera, consultas `AsNoTracking` con filtro de estado, modal de Bootstrap con POST + antiforgery.

## 9. Auditoría (~1 min)

- **Mostrar** (terminal):
  ```sql
  SELECT Id, Action, Result, TimestampUtc
  FROM AuditLogs WHERE EntityType = 'Technique' ORDER BY Id DESC;
  -- y el detalle del último:
  SELECT Details FROM AuditLogs WHERE Id = <último>;
  ```
  Deben verse las acciones `0 Create`, `1 Update`, `2 SoftDelete`, `3 Restore` con `Result = 0` y `Details` con `"Actor":"admin-local"`.
- **Decir**: "Cada operación administrativa escribe su auditoría **en la misma transacción**: si la auditoría falla, la operación se revierte (hay prueba automática de eso). Se guarda actor, acción, entidad, id, fecha UTC y resultado; nunca contraseñas ni secretos."
- **Tecnología**: contrato `IAuditService` compartido, adaptado al contexto SQL Server de la web.

## 10. Pruebas automatizadas y cierre (~1 min)

- **Mostrar** en terminal (si alcanza el tiempo, UNA sola):
  ```powershell
  dotnet test MiCaminoBJJ.Web.Tests/MiCaminoBJJ.Web.Tests.csproj --nologo
  ```
  Resultado: **130/130**. Completar con: "`dotnet test MiCaminoBJJ.Tests` → 225/225; total **355/355** y build sin errores ni advertencias."
- **Decir**: "Hay integración real contra la base exclusiva (creación, duplicados, estados, reversión de auditoría) con limpieza solo de los registros de prueba."
- **Cierre**: "Qué está pendiente y está fuera de esta demo: Identity, roles, multimedia y alias desde la web, entrenamientos en la web. Y una aclaración importante: esto es un entorno de desarrollo —no está listo para producción—, que es exactamente lo que el alcance de este TFI contempla."

---

## Mensajes de seguridad para la defensa

- "Si probás el panel en un entorno que no sea Development, no existe: responde 404."
- "La contraseña no está en el repositorio; está en User Secrets, fuera del código versionado."
- "Los formularios POST llevan token antiforgery; sin token, la respuesta es 400."
- "El admin no puede borrar físicamente técnicas: solo desactivar/reactivar."
