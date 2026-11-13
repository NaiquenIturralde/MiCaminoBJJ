# Arquitectura Web — Mi Camino BJJ

> Complemento de [README_TFI.md](README_TFI.md). Describe el diseño real del proyecto `MiCaminoBJJ.Web` tal como está implementado.

## 1. Patrón MVC

ASP.NET Core MVC separa la interacción en tres roles:

- **Model**: los datos y las reglas. En este proyecto son las entidades de `MiCaminoBJJ.Core` (`Technique`, `Belt`, `TechniqueCategory`, `Position`, `TechniqueAlias`, `AuditLog`…) más los **ViewModels** de la capa Web, que son los modelos de entrada/salida de formularios y vistas (las entidades EF **nunca** se enlazan directamente a un POST).
- **View**: plantillas Razor (`.cshtml`) que solo presentan ViewModels. No consultan bases de datos ni contienen reglas de negocio. Aplican el escapes HTML del framework (sin `Html.Raw` con datos de entrada).
- **Controller**: recibe la petición (GET/POST), aplica atributos de seguridad (`[Authorize]`, gate de Development, antiforgery), arma los ViewModels llamando a servicios por inyección de dependencias y elige la vista o el redirect.

Ejemplo de flujo de responsabilidades en `TecnicasController`: recibe `buscar/cinturon/categoria/posicion`, delega la consulta en `ITecnicasConsultaService` y arma `TecnicasIndexViewModel`; el controlador no conoce SQL.

## 2. Separación entre Core, Infrastructure y Web

```
MiCaminoBJJ.Web ─────────► MiCaminoBJJ.Infrastructure ─────────► MiCaminoBJJ.Core
   (MVC + SQL Server)          (EF Core + SQLite de MAUI)           (entidades/reglas)
```

- **Core**: entidades, enums (`AuditAction`, `AuditResult`), interfaces (`ITecnicasConsultaService`, `IAuditService`) y utilidades puras (`TextNormalizer`). Sin dependencias de infraestructura.
- **Infrastructure**: `AppDbContext` (SQLite de la app móvil), configuraciones `IEntityTypeConfiguration`, migraciones SQLite y servicios compartidos (`TechniqueService`, `AuditService`, `AdminAuthorizationService`). **La web lo reutiliza solo para las configuraciones del modelo; nunca registra `AddInfrastructure`** (eso inicializaría SQLite/MAUI).
- **Web**: vistas, controladores, ViewModels, servicios propios de consulta/comando, `WebTfDbContext`, migraciones SQL Server y el módulo administrativo. Ningún proyecto compartido referencia a Web → sin dependencias circulares.

## 3. Entity Framework Core y `WebTfDbContext`

- `WebTfDbContext : DbContext` declara los **mismos 20 DbSets** del dominio y aplica las **mismas configuraciones compartidas** con `ApplyConfigurationsFromAssembly` (una configuración nueva en Infrastructure se suma sola), sin duplicar entidades.
- **¿Un DbContext propio y no `AppDbContext`?** Porque `AppDbContext` está ligado a SQLite y a su carpeta `Migrations`; compartirlo mezclaría migraciones de dos proveedores en el mismo ensamblado.
- **Ajuste específico SQL Server**: SQL Server rechaza "multiple cascade paths" (rutas de cascada múltiples). En `WebTfDbContext` se restringe la FK `TechniquePractice → TrainingSession` (`DeleteBehavior.Restrict`) **solo en el contexto web**, sin tocar archivos compartidos.
- Todas las lecturas web usan `AsNoTracking()` (la web no modifica entidades leídas).

## 4. SQL Server y migraciones independientes

|                           | SQLite (app móvil)                          | SQL Server (web)                                  |
| ------------------------- | ------------------------------------------- | ------------------------------------------------- |
| DbContext                 | `AppDbContext`                              | `WebTfDbContext`                                  |
| Proveedor                 | `Microsoft.EntityFrameworkCore.Sqlite`      | `Microsoft.EntityFrameworkCore.SqlServer` 10.0.10 |
| Migraciones               | `MiCaminoBJJ.Infrastructure/Migrations`     | `MiCaminoBJJ.Web/Migrations/SqlServer`            |
| Ensamblado de migraciones | Infrastructure                              | Web (`MigrationsAssembly`)                        |
| Base                      | `%LocalAppData%\MiCaminoBJJ\micaminobjj.db` | `MiCaminoBJJ_Web_TFI_2026` (LocalDB)              |

- Migración web única: `20261030150827_InitialSqlServer` → 21 tablas, 26 FKs.
- **La aplicación nunca ejecuta `Database.Migrate`, `EnsureCreated` ni `EnsureDeleted`**: la migración se aplica manualmente con `dotnet ef database update` (hay pruebas que verifican que no haya migraciones pendientes).
- El seed (`WebLibrarySeeder`) solo inserta si el catálogo está vacío (merge por nombre normalizado) y corre únicamente en Development, con try/catch y log (si la base falta, la app arranca y la biblioteca muestra su mensaje de error, **nunca datos de demostración como si fueran reales**).

## 5. Servicios e inyección de dependencias

Registro en `Program.cs`:

| Servicio                                            | Vida   | Implementación                                                         |
| --------------------------------------------------- | ------ | ---------------------------------------------------------------------- |
| `ITecnicasConsultaService`                          | Scoped | `TecnicasSqlServerConsultaService` (biblioteca pública, AsNoTracking)  |
| `IAdminTecnicasQueryService`                        | Scoped | `AdminTecnicasQueryService` (listado admin, incluye desactivadas)      |
| `IAdminTecnicasCommandService`                      | Scoped | `AdminTecnicasCommandService` (altas/ediciones/estados, transaccional) |
| `IAuditService`                                     | Scoped | `WebAuditService` (adaptador del contrato Core sobre `WebTfDbContext`) |
| `IDevAdminCredentialValidator` / `IDevAdminSession` | Scoped | credencial User Secrets / cookie de Development                        |
| `WebTfDbContext`                                    | Scoped | `AddWebTfPersistence` (solo `UseSqlServer`, sin conectar al registrar) |

Separación consultas/comandos: los servicios de **consulta** son puros (`AsNoTracking`, sin escrituras); los de **comando** validan, mutan dentro de una transacción y auditan.

## 6. Validaciones

1. **DataAnnotations en ViewModels** (`AdminTecnicaFormViewModel`): nombre obligatorio ≤ 200, descripción ≤ 2000, objetivo ≤ 1000, pasos/consejos ≤ 4000, desplegables obligatorios (`Required` + `Range ≥ 1`). MVC las ejecuta en el servidor antes de tocar servicios.
2. **Reglas de dominio en el servicio de comandos**: obligatoriedad/longitudes reales de la entidad, existencia de las 3 claves foráneas y **duplicados por `TextNormalizer`** (el mismo nombre con tildes/caja distintas choca; editar la propia técnica no).
3. Errores de negocio → `ModelState` + re-render del formulario con mensajes; fallo de infraestructura → log + mensaje genérico (la transacción ya revirtió, sin datos parciales ni fugas de stack trace).

## 7. Seguridad y auditoría

- **Autenticación**: cookie estándar de ASP.NET Core (`MiCaminoBJJ.Admin`): HttpOnly, SameSite=Lax, no persistente, 8 h, `SecurePolicy=SameAsRequest`. La credencial vive en **User Secrets** (`AdminDev:Password`), se valida en el servidor con comparación de tiempo fijo y jamás se loguea.
- **Autorización**: `[Authorize]` + `AdminSoloDevelopmentAttribute` (authorization filter con `Order = -2000`): fuera de Development todo el módulo responde **404** (antes que antiforgery), sin excepción para POST.
- **Antiforgery**: `AutoValidateAntiforgeryTokenAttribute` global + `[ValidateAntiForgeryToken]` explícito en los 6 POST.
- **Cabeceras**: `X-Frame-Options: DENY` (anti-clickjacking), `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin` en todas las respuestas. La política envía el **origen** a dominios externos (nunca la ruta ni los parámetros) porque el reproductor de YouTube exige la cabecera `Referer`: con `same-origin` el navegador la omite y el embed falla con **Error 153**.
- **Overposting**: el `Id` se ignora en alta y se fija desde la ruta en edición; el ViewModel no expone `IsActive`, `NameNormalized` ni fechas (pruebas de regresión).
- **Auditoría**: `WebAuditService` implementa el contrato compartido `IAuditService` con la misma semántica del servicio original (lista blanca de claves, anti-recursión, modo tolerante/estricto). Los comandos usan `RecordStrictAsync` **dentro de la transacción**: si la auditoría falla, se revierte la operación completa. Cada operación deja `Action` (Create/Update/SoftDelete/Restore), `EntityType='Technique'`, `EntityId`, `TimestampUtc`, `Result` y `Details` con `Actor` (cookie) y los cambios Old/New.

## 8. Flujo de una consulta (pública)

```
GET /Tecnicas?buscar=RNC&cinturon=2
  → TecnicasController.Index (solo GET)
  → ITecnicasConsultaService.SearchAsync(filtro)   [Scoped]
  → TecnicasSqlServerConsultaService: AsNoTracking + Includes + Where normalizado
  → WebTfDbContext → SQL Server (MiCaminoBJJ_Web_TFI_2026)
  → List<Technique> → TecnicasIndexViewModel → Views/Tecnicas/Index.cshtml
```

Búsqueda: `TextNormalizer.Normalize(término)` del lado del servidor, comparado contra `NameNormalized` de la técnica y de sus alias (tolerante a tildes/ñ/caja) + descripción/consejos literales.

## 9. Flujo de una operación CRUD (administrativa)

```
POST /AdminTecnicas/Create  (form con token)
  → Antiforgery global + [ValidateAntiForgeryToken]         ✗ sin token = 400
  → [Authorize]                                               ✗ sin cookie = 302 a login
  → AdminSoloDevelopment (Order -2000)                        ✗ fuera de Development = 404
  → ModelState (DataAnnotations)                              ✗ inválido = re-render
  → IAdminTecnicasCommandService.CrearAsync(comando, actor)
      → validaciones de dominio (FKs, duplicados, longitudes) ✗ = errores al formulario
      → BEGIN TRANSACTION
      → INSERT Techniques (SaveChanges)
      → IAuditService.RecordStrictAsync(Create, Actor, …)     (2º SaveChanges en la MISMA tx)
      → COMMIT
  → TempData["Exito"] + RedirectToAction(Index)
  → la biblioteca pública lo muestra de inmediato (misma base)
```

Desactivar/reactivar sigue el mismo camino con `IsActive` (bandera) y acciones `SoftDelete`/`Restore`; **no existe eliminación física** en la web.
