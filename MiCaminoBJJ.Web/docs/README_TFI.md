# Mi Camino BJJ Web — Documento TFI

> Trabajo Final Integrador · Presentación: **miércoles 4 de noviembre de 2026**
> Documento técnico de referencia: [ARQUITECTURA_WEB.md](ARQUITECTURA_WEB.md) · Guía de la defensa: [GUIA_DEMOSTRACION.md](GUIA_DEMOSTRACION.md)

## 1. Nombre y objetivo del proyecto

**Mi Camino BJJ** es una plataforma educativa de Brazilian Jiu-Jitsu que reúne una biblioteca técnica de técnicas (con búsqueda por nombre y sinónimos), el registro de entrenamientos y el seguimiento del progreso de cada practicante. Este documento cubre la **versión web** (`MiCaminoBJJ.Web`), construida sobre la misma arquitectura compartida que la aplicación móvil.

**Objetivo de la versión web:** poner la biblioteca de técnicas a alcance de cualquier navegador con una interfaz aprobada (negra y dorada), persistir sus datos en SQL Server de forma independiente de la app móvil y habilitar un módulo administrativo local para mantener el catálogo.

## 2. Problema que busca resolver

- Los practicantes consultan técnicas con nombres diferentes según la academia o el idioma ("Mata león", "RNC", "Rear Naked Choke"): la búsqueda debe tolerar alias, tildes y mayúsculas.
- El material didáctico suele dispersarse en notas, redes o PDFs: se necesita una biblioteca única, filtrable por cinturón, categoría y posición.
- El progreso y las técnicas oficiales requieren un registro persistente y auditable, separado entre la app móvil (SQLite local) y la web (SQL Server).

## 3. Tecnologías utilizadas

| Capa          | Tecnología                                                                                             |
| ------------- | ------------------------------------------------------------------------------------------------------ |
| Runtime       | .NET 10                                                                                                |
| Web           | ASP.NET Core MVC (Razor Views), Bootstrap 5                                                            |
| ORM           | Entity Framework Core 10.0.10 (SQL Server provider)                                                    |
| Base de datos | SQL Server LocalDB (`(localdb)\MSSQLLocalDB`, v15.0)                                                   |
| Seguridad     | Cookie authentication (framework), Data Protection, antiforgery del framework, User Secrets            |
| Pruebas       | xUnit 2.9.3 + FluentAssertions 8.10                                                                    |
| Compartido    | `MiCaminoBJJ.Core` (entidades/reglas), `MiCaminoBJJ.Infrastructure` (EF Core + SQLite de la app móvil) |

## 4. Arquitectura de la solución

```
MiCaminoBJJ.sln
├── MiCaminoBJJ.Core            → entidades, enums, interfaces, reglas puras (TextNormalizer)
├── MiCaminoBJJ.Infrastructure  → AppDbContext (SQLite de MAUI), configuraciones, servicios compartidos, migraciones SQLite
├── MiCaminoBJJ                 → app móvil .NET MAUI Blazor Hybrid (NO modificada por la web)
├── MiCaminoBJJ.Web             → sitio MVC + persistencia SQL Server propia (WebTfDbContext) + módulo admin
├── MiCaminoBJJ.Tests           → 225 pruebas del núcleo compartido
└── MiCaminoBJJ.Web.Tests       → 130 pruebas web (unitarias + integración sobre SQL Server)
```

Regla de dependencias: `Web → Infrastructure → Core`; ningún proyecto compartido referencia a Web, por lo que no hay dependencias circulares. Detalle completo en [ARQUITECTURA_WEB.md](ARQUITECTURA_WEB.md).

## 5. Funcionalidades Web implementadas

- **Home** institucional con identidad visual negra y dorada y anclas a secciones.
- **Biblioteca de técnicas** (solo lectura, datos persistidos en SQL Server):
  - Listado con tarjetas, búsqueda por nombre/alias descripción, filtros combinados (cinturón, categoría, posición).
  - Búsqueda tolerante a tildes, ñ y mayúsculas (normalización `TextNormalizer` en ambas orillas).
  - Detalle con pasos, consejos, alias ("También conocida como") y **vuelta conservando los filtros**.
  - HTTP 404 para identificadores inexistentes.
- **Panel administrativo `AdminTecnicas`** (SOLO Development):
  - Login con credencial de desarrollo (User Secrets), cookie de ASP.NET Core, cierre de sesión.
  - Alta, edición, desactivación (con confirmación) y reactivación de técnicas; **sin eliminación física**.
  - Validaciones de dominio + detección de duplicados por nombre normalizado + antiforgery en todos los POST.
  - **Auditoría transaccional** de cada operación (Create/Update/SoftDelete/Restore con actor y fecha UTC).
- **Seguridad**: rutas admin con `[Authorize]` + gate de entorno (404 fuera de Development), cabeceras `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy`, sin credenciales en el repositorio.

## 6. Funcionalidades pendientes (NO implementadas)

- Identidad y roles web (Identity), login de usuarios finales, administración de usuarios.
- Gestión de alias y multimedia desde la web; papeleras de recuperación.
- Entrenamientos, estadísticas y progreso en la web (viven en la app móvil por ahora).
- Sincronización entre dispositivos, Docker, API REST, publicación en tiendas, IA.
- Puesta en producción (HTTPS, rate limiting, CSP completa).

## 7. Configuración de SQL Server LocalDB

- **Instancia**: `(localdb)\MSSQLLocalDB` (SQL Server 2019 LocalDB, instalada en la máquina de desarrollo).
- **Base exclusiva de la web**: `MiCaminoBJJ_Web_TFI_2026` — 21 tablas (20 entidades + `__EFMigrationsHistory`), 26 claves foráneas. Ninguna otra base se utiliza ni se modifica.
- **Cadena de conexión**: `appsettings.json` → `ConnectionStrings:MiCaminoBJJWeb` (autenticación de Windows, **sin credenciales**). Cualquier dato sensible va a User Secrets o variables de entorno.
- **Migración** (una sola vez, manual — la app NO migra automáticamente):

  ```powershell
  dotnet ef database update --project MiCaminoBJJ.Web --startup-project MiCaminoBJJ.Web --context WebTfDbContext
  ```

- **Carga inicial**: el seeder idempotente corre al arrancar en Development y carga 11 técnicas, 13 alias, 5 cinturones, 5 categorías y 5 posiciones; repetirlo no duplica datos.

### Credencial del módulo administrativo (fuera del repositorio)

```powershell
# Ver / rotar (se reinicia la app después: la configuración se lee al arrancar)
dotnet user-secrets list --project MiCaminoBJJ.Web
dotnet user-secrets set "AdminDev:Password" "<valor-nuevo>" --project MiCaminoBJJ.Web
```

### Restablecer la contraseña del administrador (Identity)

La contraseña de la cuenta web vive **solo** en `AspNetUsers.PasswordHash` (PBKDF2 de Identity).
Las credenciales de la app móvil (SQLite) y de `AdminDev:Password` **no sirven** para el login público.
No existe flujo de "olvidé mi contraseña" (sin SMTP): la recuperación es este comando de mantenimiento.

```powershell
# Solo Development y base exacta MiCaminoBJJ_Web_TFI_2026. La contraseña se escribe en
# la consola (sin eco); NUNCA por argumentos, archivos ni variables de entorno.
dotnet run --project MiCaminoBJJ.Web -- reset-admin-password
```

Garantías del comando:

- **No crea nada**: localiza la cuenta existente por email normalizado y exige
  `DomainUserId = 339`, perfil de dominio con el mismo email y rol `Administrador` **vigente**.
  Si falta cualquiera de esas condiciones, **aborta sin escribir** (el rol nunca se concede aquí).
- **Respaldo previo verificable**: `BACKUP DATABASE` + `RESTORE VERIFYONLY` en
  `Documentos\MiCaminoBJJ_respaldos\`; si el respaldo falla, no se escribe nada.
- **Una transacción**: `GeneratePasswordResetTokenAsync` + `ResetPasswordAsync` + desbloqueo
  - auditoría estricta. Rota el `SecurityStamp` (invalida las cookies ya emitidas).
- **Sin secretos en logs**: la auditoría guarda actor, operación, email, `DomainUserId`,
  verificación de rol y si se aplicó desbloqueo — nunca contraseñas, hashes ni tokens.
- **Códigos de salida**: `0` ok · `1` uso · `2` entorno · `3` base · `4` migraciones ·
  `5` entrada inválida · `6` cuenta no encontrada · `7` concurrencia · `8` vínculo de
  dominio incorrecto · `9` error interno · `10` rol Administrador ausente · `11` respaldo fallido.

## 8. Instrucciones de ejecución

```powershell
# 1) Web (Development, puerto http://localhost:5097)
dotnet run --project MiCaminoBJJ.Web

# 2) Administración (solo en Development)
#    Abrir http://localhost:5097/AdminAcceso/Login con la credencial de user-secrets.

# 3) App móvil (opcional, Windows)
dotnet build MiCaminoBJJ/MiCaminoBJJ.csproj
```

## 9. Instrucciones de pruebas

```powershell
dotnet build MiCaminoBJJ.Web/MiCaminoBJJ.Web.csproj --nologo
dotnet test MiCaminoBJJ.Tests/MiCaminoBJJ.Tests.csproj --nologo
dotnet test MiCaminoBJJ.Web.Tests/MiCaminoBJJ.Web.Tests.csproj --nologo
```

**Última verificación (Etapa 4):** build Web 0 errores / 0 advertencias (exit 0) · `MiCaminoBJJ.Tests` **225/225** (exit 0) · `MiCaminoBJJ.Web.Tests` **130/130** (exit 0) · **Total 355/355**. Las pruebas de integración web requieren LocalDB disponible (si no lo está, fallan con un mensaje claro; no se omiten en silencio).

## 10. Limitaciones actuales

- El acceso administrativo usa **una credencial compartida de desarrollo** (no Identity/roles) y existe **solo en el entorno Development**; la cookie no es `Secure` en HTTP local.
- `Techniques.NameNormalized` no tiene índice único: los duplicados se detectan en la capa de servicio (posible carrera en usos concurrentes; aceptable para el alcance actual).
- La auditoría registra `UserId = null` (aún no hay usuarios web); el actor autenticado queda en `Details["Actor"]`.
- `dotnet ef database update` debe ejecutarse manualmente en una máquina nueva.
- El sitio **no está preparado para producción** (sin HTTPS obligatorio, sin CSP completa, sin límite de intentos de login).
