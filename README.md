# Mi Camino BJJ

**Tu progreso, en cada entrenamiento.**

Plataforma educativa de Brazilian Jiu Jitsu para practicantes: biblioteca técnica con búsqueda tolerante a alias, recorrido de aprendizaje por cinturón, reproducción multimedia de técnicas, perfil del practicante y un guía conversacional con inteligencia artificial.

> **Trabajo Final Integrador (TFI) — Programación Web**
> Autora: **Naiquen Anael Iturralde**
> Repositorio: `https://github.com/NaiquenIturralde/MiCaminoBJJ` *(privado)*

---

## Índice

1. [Presentación y objetivos](#1-presentación-y-objetivos)
2. [Contexto académico](#2-contexto-académico)
3. [Tecnologías](#3-tecnologías)
4. [Arquitectura de la solución](#4-arquitectura-de-la-solución)
5. [Funcionalidades implementadas y verificadas](#5-funcionalidades-implementadas-y-verificadas)
6. [Roles y permisos](#6-roles-y-permisos)
7. [Biblioteca de técnicas y reproducción multimedia](#7-biblioteca-de-técnicas-y-reproducción-multimedia)
8. [Gestión de perfil](#8-gestión-de-perfil)
9. [Idiomas disponibles](#9-idiomas-disponibles)
10. [Guía del Practicante con IA](#10-guía-del-practicante-con-ia)
11. [Seguridad, autorizaciones, auditoría y validaciones](#11-seguridad-autorizaciones-auditoría-y-validaciones)
12. [Modelo de datos](#12-modelo-de-datos)
13. [Requisitos](#13-requisitos)
14. [Instrucciones de ejecución local](#14-instrucciones-de-ejecución-local)
15. [Configuración segura](#15-configuración-segura)
16. [Pruebas](#16-pruebas)
17. [Despliegue en MonsterASP.NET](#17-despliegue-en-monsteraspnet)
18. [Acceso para evaluación](#18-acceso-para-evaluación)
19. [URL pública definitiva](#19-url-pública-definitiva)
20. [Estructura del repositorio](#20-estructura-del-repositorio)
21. [Autora](#21-autora)

---

## 1. Presentación y objetivos

El Brazilian Jiu Jitsu exige aprender muchísimas posiciones, escapes y finalizaciones, y recordarlas durante la práctica es una de las dificultades más reales de la disciplina. Los materiales didácticos además se dispersan entre cuadernos, grupos de mensajería, vídeos sueltos y PDFs, y cada academia llama a la misma técnica de forma distinta.

**Mi Camino BJJ** nace para resolver ese problema: un espacio único donde el practicante consulta técnicas, las filtra por cinturón, categoría y posición, las reproduce en vídeo embebido, registra material propio y sigue un recorrido de aprendizaje ordenado.

### Objetivos del sistema

- **Centralizar** la información técnica de BJJ en una biblioteca consultable desde el navegador.
- **Tolerar la variación terminológica**: una misma técnica se busca como *Mata león*, *RNC*, *Rear Naked Choke* o *Cadarço*. La búsqueda ignora tildes, `ñ`, mayúsculas y alias.
- **Ordenar el aprendizaje** por cinturón, categoría y posición, con un recorrido guiado (ficha → módulo → cinturón).
- **Reproducir el material en el propio sitio** mediante vídeo embebido, sin salir de la ficha.
- **Dar identidad al practicante** con perfil editable y fotografía.
- **Ofrecer un guía conversacional** que responde consultas de técnica en lenguaje natural.
- **Separar la gestión del contenido** en un panel administrativo con trazabilidad de cada operación.
- **Sostener una base técnica coherente**: capas, migraciones, pruebas automatizadas y control de acceso por roles.

### Alcance del entregable

Este repositorio contiene la **versión web** del sistema (`MiCaminoBJJ.Web`), que es el objeto de evaluación del TFI. La aplicación móvil .NET MAUI (`MiCaminoBJJ`) comparte el núcleo de dominio y la capa de infraestructura, y se conserva como proyecto acompañante de la misma solución.

---

## 2. Contexto académico

| Dato | Detalle |
| --- | --- |
| Asignatura | Programación Web |
| Trabajo | Trabajo Final Integrador (TFI) |
| Entregable principal | Aplicación web ASP.NET Core MVC `MiCaminoBJJ.Web` |
| Base de datos | SQL Server (`MiCaminoBJJ_Web_TFI_2026`, instancia LocalDB en desarrollo) |
| Metodología | Desarrollo iterativo por etapas, con auditoría de seguridad y pruebas automatizadas en cada entrega |
| Verificación | Pruebas unitarias, de integración sobre HTTP real (pipeline) y de persistencia contra SQL Server |

El proyecto se construyó por etapas auditadas. Cada una de ellas dejó su documentación técnica en [`MiCaminoBJJ.Web/docs/`](MiCaminoBJJ.Web/docs/).

---

## 3. Tecnologías

### Stack principal (versión web)

| Área | Tecnología | Versión / detalle |
| --- | --- | --- |
| Lenguaje | C# | .NET 10 (`net10.0`) |
| Framework web | ASP.NET Core MVC | .NET 10, Razor Views |
| Vistas | Razor (`.cshtml`) | Motor por defecto de ASP.NET Core MVC |
| ORM | Entity Framework Core | 10.0.10 (proveedor SQL Server) |
| Base de datos | SQL Server | LocalDB `(localdb)\MSSQLLocalDB` en desarrollo; SQL Server gestionado en despliegue |
| Autenticación | ASP.NET Core Identity | 10.0.10 (PBKDF2, bloqueo de cuenta, roles) |
| Autorización | Políticas de ASP.NET Core | Esquemas de cookie separados (practicantes / administrador) |
| Estilos | Bootstrap 5 | 5.x, servido localmente desde `wwwroot/lib/bootstrap` |
| Validación cliente | jQuery Validation + Unobtrusive | Servidos localmente desde `wwwroot/lib/jquery*` |
| JavaScript | JavaScript (ES6, sin framework) | `wwwroot/js/site.js`, `guia.js`, `recorrido-video.js`, `password-toggle.js` |
| Localización | `IStringLocalizer` + `.resx` | Catálogo compartido es/en/pt |
| Configuración segura | User Secrets / variables de entorno | Fuera del control de versiones |
| Pruebas | xUnit + FluentAssertions + Moq | 2.9.3 / 8.10 / 4.20 |
| Integración HTTP | `Microsoft.AspNetCore.Mvc.Testing` | 10.0.10 (`WebApplicationFactory`) |
| Control de versiones | Git | — |

### Stack de la solución completa

| Proyecto | Stack |
| --- | --- |
| `MiCaminoBJJ.Web` | ASP.NET Core MVC + EF Core (SQL Server) + Identity |
| `MiCaminoBJJ.Core` | Dominio: entidades, enums, interfaces, reglas puras (`net10.0`) |
| `MiCaminoBJJ.Infrastructure` | EF Core + SQLite + migraciones (base de la app móvil) |
| `MiCaminoBJJ` | .NET MAUI Blazor Hybrid (Android, iOS, Mac Catalyst, Windows) |
| `MiCaminoBJJ.Tests` | Pruebas del núcleo compartido |
| `MiCaminoBJJ.Web.Tests` | Pruebas web (unitarias, pipeline HTTP, persistencia SQL Server) |

---

## 4. Arquitectura de la solución

```
MiCaminoBJJ.sln
│
├── MiCaminoBJJ.Core            Dominio puro: entidades, enums, interfaces,
│                               reglas de normalización y cálculo. Sin dependencias.
│
├── MiCaminoBJJ.Infrastructure  EF Core + SQLite (base local de la app móvil),
│                               configuraciones de mapeo, seed y servicios compartidos.
│
├── MiCaminoBJJ                 App móvil .NET MAUI Blazor Hybrid.
│                               Consume Core + Infrastructure. SQLite local.
│
├── MiCaminoBJJ.Web             ENTREGABLE DEL TFI.
│   ├── Controllers/            MVC + endpoint JSON del chat del guía
│   ├── Models/ ViewModels/     Contratos de entrada/salida de las vistas
│   ├── Persistence/            WebTfDbContext (SQL Server) + opciones + auditoría
│   ├── Security/               Identity, políticas, bootstrap admin, cabeceras
│   ├── Services/               Servicios de aplicación (Catálogo, Guía, Admin, Perfil)
│   ├── Localization/           Catálogo es/en/pt y middleware de preferencia
│   ├── Resources/              Archivos .resx traducidos
│   ├── Views/                  Vistas Razor
│   ├── Migrations/SqlServer/   Migraciones EF Core de la base de la web
│   └── wwwroot/                Bootstrap, jQuery, CSS y JS propios
│
├── MiCaminoBJJ.Tests           Pruebas del núcleo y de la infraestructura
└── MiCaminoBJJ.Web.Tests       Pruebas web: unitarias + pipeline HTTP real + SQL Server
```

### Reglas de arquitectura

- **Dependencias unidireccionales**: `Web → Infrastructure → Core`. Ningún proyecto compartido referencia a `Web`, por lo que no existen dependencias circulares.
- **Aislamiento de persistencia**: la web usa su propio `WebTfDbContext` sobre SQL Server; la app móvil usa `AppDbContext` sobre SQLite. **La web nunca modifica la base de la app móvil ni viceversa.**
- **Composición explícita en `Program.cs`**: cada servicio se registra de forma explícita y justificada; no hay cableado automático por convención.
- **Capas dentro de la web**: `Controllers → Services (aplicación) → Persistence / Core`. Los controladores no escriben directamente con EF Core: usan servicios de comando y de consulta separados.
- **Recursos de interfaz desacoplados del modelo**: los controladores devuelven `ViewModel`s; las vistas no reciben entidades de EF Core.

---

## 5. Funcionalidades implementadas y verificadas

Todas las funcionalidades de esta sección **están implementadas y cubiertas por pruebas automatizadas**. Lo que no figura aquí no está terminado.

### 5.1. Sitio público

- **Home institucional** con identidad visual y llamada a la acción a las secciones principales.
- **Ayuda** (`/Ayuda`) con documentación de uso de la plataforma.

### 5.2. Biblioteca de técnicas

- **Listado** en tarjetas con portada, cinturón, categoría y posición.
- **Búsqueda** por nombre, alias y descripción, con normalización en ambos extremos (`TextNormalizer`): ignora tildes, `ñ`, mayúsculas y acentos.
- **Filtros combinados** por cinturón, categoría y posición, acumulables.
- **Ficha de detalle** (`/Tecnicas/Details/{id}`) con pasos, consejos, alias («También conocida como»), recursos multimedia y guías de avance.
- **HTTP 404** para identificadores inexistentes.
- **Conservación de filtros** al volver del detalle al listado.

### 5.3. Recorrido de aprendizaje por cinturón

- **Cinturón blanco** (`/recorrido/cinturon-blanco`): catálogo de módulos y fichas ordenadas.
- **Módulo** (`/recorrido/cinturon-blanco/modulo/{slug}`): índice de las fichas del módulo.
- **Ficha** (`/recorrido/cinturon-blanco/ficha/{slug}`): contenido ampliado con vídeo demostrativo y referencias a la biblioteca.
- Las fichas del recorrido **enlazan con la biblioteca real**: no hay contenido duplicado ni datos de demostración.

### 5.4. Cuentas de practicante

- **Registro** público con validación de datos y de contraseña (`/Cuenta/Registro`).
- **Inicio y cierre de sesión** (`/Cuenta/Login`, `/Cuenta/Logout`), con cookie de sesión propia (`MiCaminoBJJ.Cuenta`) y expiración no persistente.
- **Página de acceso denegado** (`/Cuenta/AccesoDenegado`) para sesión válida sin permisos suficientes.
- **Menú de usuario y barra de navegación** que reaccionan al estado de sesión.

### 5.5. Perfil del practicante

- **Edición de perfil** (`/Cuenta/Perfil`): nombre y cinturón.
- **Fotografía de perfil** con carga de imagen, validación en servidor (tipo MIME, tamaño y contenido real) y entrega controlada: el archivo se almacena **fuera de `wwwroot`** y solo su propietario puede descargarlo (`/Cuenta/FotoPerfil/{id}`).

### 5.6. Configuración

- **Preferencia de idioma** (`/Cuenta/Configuracion`), persistida por usuario y respaldada por cookie para visitantes anónimos.

### 5.7. Guía del Practicante (IA)

- **Chat conversacional** (`/GuiaPracticante`) con endpoint JSON propio, accesible solo a usuarios autenticados.
- **Dos modos de respuesta**, etiquetados de forma explícita en la interfaz para no presentar el catálogo como IA:
  - **Modo IA**: cuando hay proveedor configurado.
  - **Modo catálogo**: respuesta determinista sobre la biblioteca real cuando el proveedor no está configurado o falla.
- **Fallback automático** ante timeout o error del proveedor: la consulta se responde con catálogo y se informa la nota correspondiente.

### 5.8. Panel administrativo

- **Acceso administrativo** (`/AdminAcceso/Login`) con credencial de desarrollo, cookie propia (`MiCaminoBJJ.Admin`) y cierre de sesión.
- **CRUD de técnicas** (`/AdminTecnicas`): alta, edición, **desactivación lógica** (sin borrado físico) y reactivación.
- **Gestión de multimedia** por técnica (`/AdminTecnicasMedia`): agregar, editar, marcar como portada y quitar; con validación de URL y detección de duplicados.
- **Auditoría** de cada operación de escritura.

### 5.9. Base de datos

- **Migraciones EF Core** versionadas para SQL Server (`Migrations/SqlServer/`).
- **Carga inicial idempotente** del catálogo en Development, que registra el error en el log y **no** degrada a datos falsos si la base no está disponible.
- **Importación de técnicas** mediante script SQL versionado (`doc/import/`).

---

## 6. Roles y permisos

El sistema separa dos universos de autenticación que **nunca se mezclan**, mediante esquemas de cookie distintos y políticas de autorización explícitas.

| Rol | Origen | Ámbito | Puede |
| --- | --- | --- | --- |
| **Practicante** | Registro público | Esquema `Identity.Application` (cookie `MiCaminoBJJ.Cuenta`) | Consultar la biblioteca y el recorrido, ver y editar su perfil, cambiar su idioma, usar la Guía del Practicante |
| **Administrador** | Comando local `init-admin` + rol `Administrador` de Identity | Esquema `Cookies` (cookie `MiCaminoBJJ.Admin`) | Gestionar el catálogo de técnicas y su multimedia |
| **Administrador local (Development)** | Credencial en User Secrets, comparada en servidor con tiempo fijo | Esquema `Cookies` | Igual que Administrador, **solo en el entorno de desarrollo** |
| Visitante anónimo | — | — | Home, Ayuda, biblioteca y recorrido en solo lectura |

### Garantías de aislamiento

- Ninguna ruta del proyecto usa `[Authorize]` sin política: la revisión se verifica por reflexión en las pruebas de seguridad.
- La política `AdminLocal` está **anclada explícitamente al esquema `Cookies`**, de modo que una cookie de Identity de practicante jamás autoriza el panel administrativo.
- La política de practicante está anclada al esquema `Identity.Application`.
- El panel administrativo devuelve **404 fuera de Development** (`AdminSoloDevelopmentAttribute`), sin revelar la existencia del recurso.
- El rol se asigna **solo en el servidor**: el registro público nunca acepta un rol enviado por el cliente.

---

## 7. Biblioteca de técnicas y reproducción multimedia

### Datos y búsqueda

La biblioteca se persiste en SQL Server y se consulta en **solo lectura** (`AsNoTracking`) a través de un servicio de consulta (`ITecnicasConsultaService`) separado del de escritura. No hay *fallback* a datos en memoria: si la base no responde, la vista muestra el error, nunca información falsa.

La búsqueda se apoya en un modelo de **alias** (`TechniqueAliases`) que permite que un mismo movimiento se encuentre por cualquiera de sus denominaciones, y en la **normalización de texto** (`TextNormalizer`), aplicada tanto al dato almacenado (`NameNormalized`) como al término de búsqueda.

### Multimedia

El alta de material multimedia admite **vídeo externo** de **YouTube** y **Vimeo**. Un parser del núcleo (`ExternalMediaParser`, con regex generadas) reconoce las variantes de URL (`youtube.com/watch?v=`, `youtu.be/`, `youtube.com/shorts/`, `youtube.com/embed/`, `vimeo.com/{id}`) y normaliza la URL a un **embed seguro**, en lugar de incrustar la URL original sin validar.

En la ficha, el vídeo se reproduce mediante `iframe` con `sandbox` y `referrerpolicy` restringidos. La integración de YouTube **no reproduce audio de fondo** ni inserta contenido de terceros en el resto de la interfaz.

### Auditoría del contenido

Toda alta, edición, desactivación, reactivación y operación sobre multimedia se registra en `AuditLog` con **actor, operación, entidad afectada, resultado y fecha UTC**, dentro de la misma transacción que la operación de negocio. El registro de auditoría nunca incluye contraseñas, tokens ni secretos.

---

## 8. Gestión de perfil

| Capacidad | Implementación |
| --- | --- |
| Datos editables | Nombre y cinturón, validados en servidor |
| Fotografía | Carga con validación de tipo MIME, tamaño y contenido real |
| Almacenamiento | `App_Data/fotos-perfil`, **fuera de `wwwroot`** y fuera del control de versiones |
| Acceso | Solo el propietario puede descargar su foto (`/Cuenta/FotoPerfil/{id}`) |
| Preferencia de idioma | Persistida en `UserSettings`, con cookie de respaldo para anónimos |
| Modelo de datos | Reutiliza `MediaAsset` + `UserProfileMedia`: **no se crean tablas nuevas** |

---

## 9. Idiomas disponibles

La web está localizada en **tres idiomas**: español (`es`), inglés (`en`) y portugués (`pt`).

### Cobertura real (verificada)

| Aspecto | Estado real |
| --- | --- |
| Catálogo de recursos | **453 claves** de interfaz en `Resources/Localization.UiResources.resx` |
| Traducciones | Los tres catálogos (`resx`, `.en.resx`, `.pt.resx`) tienen **453 claves cada uno, con el mismo conjunto de claves** (0 faltantes entre idiomas) |
| Vistas localizadas | **24 vistas Razor** consumen el catálogo compartido mediante `@L["Clave"]`, inyectado en `_ViewImports.cshtml` |
| Mensajes de validación | Los `ErrorMessage` de DataAnnotations se resuelven contra el mismo catálogo (`DataAnnotationLocalizerProvider`) |
| Idioma por defecto | Español (`es`) |
| Resolución de cultura | `UseRequestLocalization`: cookie → cabecera `Accept-Language` → español |
| Preferencia persistida | `PreferenciaIdiomaMiddleware`, que aplica el idioma guardado en `UserSettings.Language` y es la fuente de verdad para usuarios autenticados |
| Idiomas en la UI | El catálogo `Idiomas` es la **única fuente de verdad**: la vista de Configuración, el POST de guardado, el proveedor de cultura y la persistencia usan la misma lista, por lo que no puede existir un idioma «guardado» que la aplicación no sepa aplicar |

> **Alcance honesto:** la localización alcanza el **100 % de las claves del catálogo de interfaz y de los mensajes de validación**. Los **contenidos editoriales de la base de datos** (descripciones de técnicas, pasos y consejos) están en español y **no están traducidos**: la traducción de contenido es una limitación conocida y deliberada del alcance actual.

---

## 10. Guía del Practicante con IA

El guía conversacional permite preguntar, en lenguaje natural, sobre técnicas de BJJ y obtener una respuesta contextualizada.

### Proveedor utilizado

| Aspecto | Detalle |
| --- | --- |
| Motor | **Ollama** ejecutado en local (`http://localhost:11434`) |
| Endpoint | `POST /v1/chat/completions` (**API compatible con OpenAI**) |
| Modelo | `gemma3:1b` |
| Transporte | `ProveedorIaCliente`, cliente HTTP tipado con `Authorization: Bearer` |
| Parámetros | `temperature = 0.4`, `max_tokens` acotado por `GuiaLimites` |
| Timeout | Acotado y separado del timeout global de `HttpClient` |

> La elección de un motor **local** evita exponer datos de usuarios a servicios externos y permite la evaluación sin consumo de créditos ni clave de un proveedor comercial.

### Funcionamiento y degradación segura

1. Si el proveedor **no está configurado**, el guía opera en **modo catálogo** y la interfaz lo indica de forma explícita: nunca se presenta una respuesta del catálogo como si fuera generada por IA.
2. En **modo catálogo**, la respuesta se construye a partir de las técnicas **reales** de la base de datos, con sugerencias de navegación.
3. Ante **timeout** o **fallo** del proveedor, la consulta **no se interrumpe**: se responde con el catálogo y se muestra una nota que explica el motivo. El error se registra en el log **sin** incluir la clave, el endpoint ni el cuerpo de la petición.
4. La clave de API viaja **solo** en la cabecera `Authorization` de la petición saliente. **Nunca** se versiona, **nunca** se registra en logs y **nunca** se envía al navegador.

### Privacidad

El guía es una de las pocas áreas restringidas a usuarios autenticados (`UsuarioAutenticado`). El contexto enviado al proveedor se limita al guion del sistema y a la pregunta del usuario.

---

## 11. Seguridad, autorizaciones, auditoría y validaciones

### Controles implementados

| Control | Implementación |
| --- | --- |
| Antifalsificación | `AutoValidateAntiforgeryTokenAttribute` global: **todos** los POST/PUT/DELETE exigen token; además cada POST declara `[ValidateAntiForgeryToken]` explícitamente |
| Hash de contraseñas | PBKDF2 estándar de ASP.NET Core Identity. Sin texto plano ni cifrado reversible |
| Política de contraseñas | Mínimo 8 caracteres, con mayúscula, minúscula, dígito y carácter no alfanumérico |
| Bloqueo de cuenta | 5 intentos fallidos → bloqueo temporal de 5 minutos, también para usuarios nuevos |
| Email único | `RequireUniqueEmail = true` |
| Cookies | Dos cookies con nombre propio y esquemas separados; `SlidingExpiration` desactivado y `IsPersistent = false` al iniciar sesión: cerrar el navegador **no** conserva la sesión |
| Cierre de sesión | `SignOutAsync` + borrado explícito de las cookies de sesión y de autenticación |
| Credenciales de administrador | Fuera del repositorio (User Secrets). Comparación en servidor con `CryptographicOperations.FixedTimeEquals` |
| Credencial de admin en el navegador | Nunca viaja: solo se emite la cookie firmada tras validarla en el servidor |
| Cabeceras de seguridad | `X-Frame-Options: DENY` (anti-clickjacking), `X-Content-Type-Options: nosniff`, `Referrer-Policy` acotado, aplicadas a **todas** las respuestas mediante middleware |
| Gestión de errores | `UseExceptionHandler` + HSTS fuera de Development |
| Autorización | Políticas explícitas con esquema anclado; sin `[Authorize]` «desnudo» (verificado por reflexión) |
| Exposición del panel admin | 404 fuera de Development |
| Operandos locales | `init-admin` y `reset-admin-password` solo en Development, con verificación previa del nombre exacto de la base autorizada y **candado de base de datos** (`sp_getapplock`) contra ejecuciones concurrentes |
| Atomicidad del alta de administrador | Identity + rol + perfil de dominio + auditoría, en **una sola transacción** |
| Restablecimiento de contraseña | Usa la API oficial de Identity (`GeneratePasswordResetToken` + `ResetPassword`), con verificación previa del rol y del vínculo de dominio |

### Validaciones

- **De modelo**: DataAnnotations en ViewModels, con mensajes de error traducidos al idioma activo.
- **De dominio**: reglas en la capa de aplicación (por ejemplo, detección de duplicados por nombre normalizado).
- **De entrada**: las URLs de multimedia se parsean y se normalizan a un embed válido antes de persistirse.
- **De archivos**: la fotografía de perfil valida tipo MIME, tamaño y contenido real.
- **De base de datos**: `AsNoTracking` en las consultas de lectura y transacciones explícitas en las de escritura.

### Auditoría

`AuditLog` registra las operaciones de escritura con **actor, acción, entidad, resultado y fecha UTC**. Se cubren, entre otras, las operaciones de administración de técnicas y multimedia, el alta del administrador y las operaciones de perfil. El registro **nunca** incluye contraseñas, tokens ni secretos.

---

## 12. Modelo de datos

La base de la web (`MiCaminoBJJ_Web_TFI_2026`) se modela con Entity Framework Core sobre **21 tablas** (20 entidades + `__EFMigrationsHistory`).

### Entidades principales

| Grupo | Entidades |
| --- | --- |
| Catálogo | `Technique`, `TechniqueCategory`, `Belt`, `Position`, `TechniqueAlias` |
| Multimedia | `MediaAsset`, `TechniqueMedia` |
| Usuarios | `User` (extensión de `IdentityUser`), `UserProfileMedia`, `UserSettings` |
| Actividad del practicante | `SavedTechnique`, `TechniqueView`, `TechniquePractice`, `UserTechniqueProgress`, `UserTechniqueResource`, `TrainingSession` |
| Logros y torneos | `Achievement`, `UserAchievement`, `UserTournament` |
| Seguridad | `AuditLog` |

### Diagramas

Los diagramas del modelo se versionan en [`doc/Diagramas/`](doc/Diagramas/):

| Archivo | Contenido |
| --- | --- |
| `DER.png` | Diagrama entidad-relación |
| `modeloER.png` | Modelo entidad-relación |
| `DiccionarioDatos.png` | Diccionario de datos |
| `CasosDeUso.png` | Diagrama de casos de uso |

### Migraciones

- `MiCaminoBJJ.Web/Migrations/SqlServer/` — migraciones de la **base de la web** (SQL Server).
- `MiCaminoBJJ.Infrastructure/Migrations/` — migraciones de la **base local de la app móvil** (SQLite).

Son bases **independientes**: las migraciones de una nunca se aplican sobre la otra.

---

## 13. Requisitos

### Para la versión web (entregable del TFI)

| Requisito | Versión mínima |
| --- | --- |
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0 |
| SQL Server | LocalDB (incluido con Visual Studio 2022 / .NET) o SQL Server 2019+ |
| Entity Framework Core Tools | 10.0.10 (`dotnet tool install --global dotnet-ef --version 10.0.10`) |

### Para la app móvil (proyecto acompañante)

| Requisito | Detalle |
| --- | --- |
| .NET MAUI workload | `dotnet workload install maui` |
| SDK de plataforma | Android (API 24+), iOS 15+, Mac Catalyst 15+, Windows 10.0.17763+ |
| Emulador o dispositivo | Para ejecutar la app móvil |

---

## 14. Instrucciones de ejecución local

### 14.1. Restaurar y compilar

````bash
# Clonar
git clone https://github.com/NaiquenIturralde/MiCaminoBJJ.git
cd MiCaminoBJJ

# Restaurar paquetes
dotnet restore MiCaminoBJJ.sln

# Compilar la versión web
dotnet build MiCaminoBJJ.Web/MiCaminoBJJ.Web.csproj
````

### 14.2. Crear la base de datos y aplicar migraciones

````bash
# Crear la base en la instancia LocalDB (una sola vez)
sqllocaldb create MSSQLLocalDB

# Aplicar las migraciones de la web
dotnet ef database update --project MiCaminoBJJ.Web/MiCaminoBJJ.Web.csproj
````

> La cadena de conexión de la web es **independiente** de la base SQLite de la app móvil. `dotnet ef` debe ejecutarse **siempre** con `--project MiCaminoBJJ.Web/MiCaminoBJJ.Web.csproj`.

### 14.3. Configurar los secretos (ver §15)

### 14.4. Ejecutar

````bash
# Perfil HTTP de desarrollo (http://localhost:5097)
dotnet run --project MiCaminoBJJ.Web/MiCaminoBJJ.Web.csproj --launch-profile http

# Perfil HTTPS (https://localhost:7024)
dotnet run --project MiCaminoBJJ.Web/MiCaminoBJJ.Web.csproj --launch-profile https
````

También disponible en Visual Studio Code: **F5** → perfil *Mi Camino BJJ Web*.

### 14.5. Crear el administrador (opcional, solo Development)

````bash
dotnet run --project MiCaminoBJJ.Web/MiCaminoBJJ.Web.csproj -- init-admin
````

El comando **no** muestra la contraseña ni la acepta por argumentos: la lee de forma interactiva y sin eco. Crea el usuario, le asigna el rol `Administrador` y registra la operación en la auditoría, todo en una sola transacción.

Para restablecer la contraseña de un administrador existente:

````bash
dotnet run --project MiCaminoBJJ.Web/MiCaminoBJJ.Web.csproj -- reset-admin-password
````

### 14.6. Cargar el catálogo inicial (opcional)

En Development, la aplicación ejecuta una **carga inicial idempotente** de la biblioteca al arrancar. Si la base no está disponible, el error queda registrado en el log y la aplicación **arranca igual**, mostrando el mensaje de error en lugar de datos falsos.

Para una carga más completa, está disponible el script versionado en [`doc/import/`](doc/import/).

---

## 15. Configuración segura

**Ninguna credencial vive en el repositorio.** `appsettings.json` contiene únicamente una cadena de conexión de **ejemplo** con `Trusted_Connection=True` (sin usuario ni contraseña).

### Opción A — User Secrets (desarrollo local)

User Secrets se almacena **fuera del árbol del repositorio**, en el perfil del usuario, y **no** se versiona.

````bash
cd MiCaminoBJJ.Web

# Credencial del administrador local (Development)
dotnet user-secrets set "AdminDev:Password" "<contraseña-local-del-admin>"
dotnet user-secrets set "AdminDev:User" "admin-local"

# Proveedor de IA del Guía del Practicante
dotnet user-secrets set "GuiaPracticante:ApiKey"   "<clave-del-proveedor>"
dotnet user-secrets set "GuiaPracticante:Endpoint" "http://localhost:11434/v1/chat/completions"
dotnet user-secrets set "GuiaPracticante:Modelo"   "gemma3:1b"
````

Para consultar qué claves están definidas **sin mostrar sus valores**:

````powershell
$id = (Get-Content MiCaminoBJJ.Web.csproj -Raw | Select-String -Pattern 'UserSecretsId>([^<]+)<').Matches[0].Groups[1].Value
(Get-Content "$env:APPDATA\Microsoft\UserSecrets\$id\secrets.json" -Raw | ConvertFrom-Json).PSObject.Properties.Name
````

### Contraseña del administrador sembrado en la app móvil

La app móvil crea, en su base **SQLite local**, una cuenta de administrador para poder gestionar las técnicas desde el dispositivo. **Esa contraseña no es un literal en el código**: se resuelve en tiempo de ejecución desde la **misma clave `AdminDev:Password`** mediante `SeedAdminPassword` (clase `MiCaminoBJJ.Infrastructure`).

| Escenario | Resultado |
| --- | --- |
| `AdminDev:Password` configurada | El admin sembrado usa esa contraseña |
| Sin configuración | Se genera una contraseña **aleatoria** (PBKDF2 + salt propio): la cuenta existe, pero **no hay credencial fija publicada en el repositorio** |

Para fijar una contraseña conocida en la app móvil:

````bash
# Windows / escritorio
set AdminDev__Password=<contraseña-elegida>
````

> En una **instalación nueva** de la app móvil, si no se define `AdminDev:Password`, el administrador sembrado queda con una contraseña aleatoria desconocida. El dispositivo ya instalado mantiene su cuenta original, porque el seed solo se ejecuta cuando la base está vacía. La cuenta de administración del **sitio web** es independiente y se crea con el comando `init-admin` (§14.5).

### Opción B — Variables de entorno (despliegue)

En MonsterASP.NET u otro hosting, la configuración se inyecta por variable de entorno, sin tocar el código:

| Variable | Contenido |
| --- | --- |
| `ConnectionStrings__MiCaminoBJJWeb` | Cadena de conexión completa de SQL Server del servidor |
| `AdminDev__Password` | *(solo si el entorno es Development)* |
| `GuiaPracticante__ApiKey` | Clave del proveedor de IA |
| `GuiaPracticante__Endpoint` | URL del endpoint `chat/completions` |
| `GuiaPracticante__Modelo` | Identificador del modelo |
| `ASPNETCORE_ENVIRONMENT` | `Production` en el servidor |

> La clave `AdminDev:Password` **solo tiene efecto en Development**. En `Production` el panel administrativo devuelve 404 y la credencial es irrelevante. Para producción se usa una cuenta real de Identity con rol `Administrador`, creada con `init-admin` durante la ventana de despliegue.

---

## 16. Pruebas

Dos proyectos de pruebas cubren la solución:

| Proyecto | Alcance | Resultado |
| --- | --- | --- |
| `MiCaminoBJJ.Tests` | Dominio y núcleo compartido: normalización de texto, cálculo de progreso, parser de media, auditoría, orden de inicialización y migración | **236 / 236 superadas** |
| `MiCaminoBJJ.Web.Tests` | Web: controladores, **pipeline HTTP real** con `WebApplicationFactory`, persistencia sobre SQL Server, seguridad, Identity, localización, guía, recorrido y administración | **468 / 468 superadas** |
| **Total** | | **704 / 704 superadas · 0 fallidas · 0 omitidas** |

### Tipos de prueba

- **Unitarias**: lógica de servicios, ViewModels y reglas de dominio, con Moq para los colaboradores.
- **Integración de pipeline HTTP**: arrancan la aplicación real en memoria y verifican el comportamiento efectivo del pipeline — cookies, antifalsificación, políticas de autorización, cabeceras de seguridad, aislamiento entre esquemas y redirecciones.
- **Integración de persistencia**: escritura y lectura reales contra SQL Server, con **base de pruebas aislada** y candado (`sp_getapplock`) para impedir que las pruebas toquen la base de desarrollo.

````bash
# Suite completa
dotnet test MiCaminoBJJ.sln

# Solo la web
dotnet test MiCaminoBJJ.Web.Tests/MiCaminoBJJ.Web.Tests.csproj

# Solo el núcleo
dotnet test MiCaminoBJJ.Tests/MiCaminoBJJ.Tests.csproj
````

> Las pruebas de persistencia requieren una instancia de SQL Server disponible. Sin ella, la suite web se salta esas pruebas de integración en lugar de ejecutarlas contra una base de desarrollo por error.

---

## 17. Despliegue en MonsterASP.NET

Instrucciones **generales**. Los valores concretos de conexión y despliegue se entregan de forma privada.

### 17.1. Preparación

1. Publicar el proyecto web: `dotnet publish MiCaminoBJJ.Web/MiCaminoBJJ.Web.csproj -c Release`.
2. Verificar que el `.csproj` **no** arrastra la base SQLite de la app móvil: la web despliega solo `MiCaminoBJJ.Web`.
3. En MonsterASP.NET, crear la aplicación **.NET** apuntando a la carpeta publicada y a la versión **.NET 10**.

### 17.2. Base de datos

1. Crear la base de datos SQL Server en el panel de MonsterASP.NET y anotar el nombre.
2. Construir la **cadena de conexión** de producción con `Server=...;Database=...;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True`.
3. Configurarla como **variable de entorno** `ConnectionStrings__MiCaminoBJJWeb` (nunca en un archivo del repositorio).
4. Aplicar las migraciones desde el equipo de desarrollo contra la base de producción:
   `dotnet ef database update --project MiCaminoBJJ.Web/MiCaminoBJJ.Web.csproj`.
   Alternativa: ejecutar el script SQL versionado en `MiCaminoBJJ.Web/Migrations/SqlServer/`.

### 17.3. Configuración del servidor

| Aspecto | Valor recomendado |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| Cadena de conexión | Variable de entorno `ConnectionStrings__MiCaminoBJJWeb` |
| Credencial de IA | Variables `GuiaPracticante__*` (o deshabilitar el modo IA y dejar el modo catálogo) |
| HTTPS | Gestionado por el panel; la aplicación ya redirige a HTTPS y activa HSTS fuera de Development |
| Datos Protected | Claves persistentes entre despliegues, si el hosting lo permite |

### 17.4. Administrador en producción

Durante la ventana de despliegue, crear el administrador desde el servidor con la variable de entorno `ASPNETCORE_ENVIRONMENT=Development` **de forma temporal** y ejecutando `init-admin`; después, restaurar `Production`. El panel administrativo permanece inaccesible en `Production`.

### 17.5. Verificación posterior

- La home carga y la base responde (la biblioteca **no** muestra el mensaje de error de conexión).
- El registro y el inicio de sesión de un practicante funcionan.
- La tabla `AuditLog` registra las operaciones.
- `https://<dominio>/Ayuda` y `https://<dominio>/recorrido/cinturon-blanco` responden.

---

## 18. Acceso para evaluación

> ### 🔒 Credenciales de evaluación
>
> **Las credenciales NO se publican en este repositorio.**
>
> Las cuentas de evaluación (practicante y administrador) se **entregan de forma privada y directa a la docente**, por un canal seguro y fuera de GitHub, junto con la URL pública definitiva (§19).
>
> El repositorio contiene **cero contraseñas**: ninguna credencial está versionada, ni en el código, ni en `appsettings.json`, ni en el historial de Git.
>
> Cuando el docente disponga de las credenciales, documentar aquí **únicamente** los datos no sensibles:
>
> | Dato | Valor |
> | --- | --- |
> | URL pública | *(ver §19)* |
> | Rol / tipo de cuenta | Practicante · Administrador |
> | Contraseña | **Entregada privadamente por canal seguro — no incluir en este archivo** |
>
> **Nunca** se deben añadir contraseñas, tokens ni claves a este archivo.

---

## 19. URL pública definitiva

> **Pendiente de confirmar.** Reemplazar este bloque cuando la publicación esté definitiva.

| Aspecto | Valor |
| --- | --- |
| URL pública de la aplicación | `https://<URL-PUBLICA-DEFINITIVA>` |
| Hosting | MonsterASP.NET |
| Base de datos | SQL Server gestionado (nombre de la base por confirmar) |
| Estado | ⏳ Pendiente de publicación |

---

## 20. Estructura del repositorio

```
MiCaminoBJJ/
├── README.md                     Este documento
├── MiCaminoBJJ.sln               Solución de Visual Studio
├── .gitignore                    Reglas de exclusión (compilación, secretos, datos)
│
├── MiCaminoBJJ.Web/              Entregable del TFI
│   ├── Controllers/              Controladores MVC + endpoint JSON del guía
│   ├── Models/ ViewModels/       Contratos de vistas
│   ├── Persistence/              DbContext SQL Server, opciones, auditoría
│   ├── Security/                 Identity, políticas, CLI de administrador
│   ├── Services/                 Catálogo, Guía, Administración, Perfil
│   ├── Localization/             Catálogo es/en/pt y middleware de idioma
│   ├── Resources/                .resx traducidos
│   ├── Views/                    Vistas Razor
│   ├── Filters/                  Atributo de entorno Development
│   ├── Migrations/SqlServer/     Migraciones de la base de la web
│   ├── docs/                     Documentación técnica del TFI
│   └── wwwroot/                  Bootstrap, jQuery, CSS y JS propios
│
├── MiCaminoBJJ.Core/             Dominio puro (entidades, enums, interfaces, reglas)
├── MiCaminoBJJ.Infrastructure/   EF Core + SQLite de la app móvil + servicios
├── MiCaminoBJJ/                  App móvil .NET MAUI Blazor Hybrid
├── MiCaminoBJJ.Tests/            Pruebas del núcleo
├── MiCaminoBJJ.Web.Tests/        Pruebas web
│
├── doc/                          Diagramas, script de importación y respaldos
└── Elementos/                    Mockups de diseño y recursos gráficos
```

### Qué **no** se publica

El `.gitignore` excluye del repositorio, entre otros:

| Categoría | Patrón |
| --- | --- |
| Compilación | `bin/`, `obj/`, `artifacts/`, `*.dll`, `*.pdb` |
| IDE | `.vs/`, `*.user`, `*.suo`, `.idea/` |
| Secretos | `*.pfx`, `*.p12`, `*.key`, `secrets*.json`, `appsettings.*.Local.json`, `.env` |
| Datos en ejecución | `**/App_Data/` (fotografías de perfil) |
| Bases de datos | `*.db`, `*.sqlite`, `*.mdf`, `*.ldf` |
| Respaldos | `*.bak` |
| Perfiles de publicación | `*.pubxml`, `*.pubxml.user` |
| Empaquetado móvil | `*.apk`, `*.aab`, `*.ipa`, `*.msix` |
| Fotografías personales | `Elementos/img/*.jpeg` (material privado conservado solo en local) |
| Artefactos de trabajo | `_tmp_*`, `*.patch`, `TestResults/` |
| Credenciales fijas | Ninguna: la contraseña del admin sembrado se resuelve desde la configuración (§15) |

### Limpieza de seguridad del historial

Antes de la primera publicación, el historial se depuró para eliminar material que no debe distribuirse:

| Elemento retirado | Motivo |
| --- | --- |
| 4 fotografías personales de `Elementos/img/` | Imágenes de personas identificables (incluidos menores) tomadas en clases: material privado, sin uso por la aplicación |
| Contraseña fija del administrador sembrado | Credencial versionada en el código; ahora se resuelve desde `AdminDev:Password` o se genera de forma aleatoria |

La limpieza se aplicó **a todos los commits** del historial publicado, no solo al estado actual. Los commits originales se conservan íntegros en una **copia de seguridad local fuera del repositorio**.

### Documentación técnica adicional

| Documento | Contenido |
| --- | --- |
| [`MiCaminoBJJ.Web/docs/ARQUITECTURA_WEB.md`](MiCaminoBJJ.Web/docs/ARQUITECTURA_WEB.md) | Arquitectura de la versión web |
| [`MiCaminoBJJ.Web/docs/GUIA_DEMOSTRACION.md`](MiCaminoBJJ.Web/docs/GUIA_DEMOSTRACION.md) | Guía de defensa del TFI |
| [`MiCaminoBJJ.Web/docs/README_TFI.md`](MiCaminoBJJ.Web/docs/README_TFI.md) | Documento de presentación del TFI |
| [`MiCaminoBJJ.Web/docs/sql/`](MiCaminoBJJ.Web/docs/sql/) | Scripts SQL de carga del catálogo |

---

## 21. Autora

**Naiquen Anael Iturralde**

Estudiante de Desarrollo de Software y practicante de Brazilian Jiu Jitsu. *Mi Camino BJJ* nace de la necesidad propia de organizar el aprendizaje de la disciplina, y del anhelo de convertirlo en una herramienta que sirva a otros practicantes.

El proyecto se entrega como **Trabajo Final Integrador de Programación Web**.

---

<div align="center">

**Mi Camino BJJ** · *Tu progreso, en cada entrenamiento.*

</div>
