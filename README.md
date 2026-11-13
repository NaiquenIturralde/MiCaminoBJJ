# Mi Camino BJJ

**Plataforma web de aprendizaje y consulta de técnicas de Brazilian Jiu-Jitsu**

**Trabajo Final Integrador — Programación Web**

Desarrollado por **Naiquen Anael Iturralde**.

---

## Descripción del proyecto

Mi Camino BJJ es una plataforma orientada a practicantes de Brazilian Jiu-Jitsu que centraliza material educativo, técnicas, recursos multimedia y herramientas de consulta.

Su objetivo es facilitar el acceso al conocimiento técnico, organizar los contenidos según el nivel del practicante y proporcionar una experiencia de aprendizaje intuitiva, accesible y adaptable a diferentes dispositivos.

La versión web fue desarrollada como Trabajo Final Integrador de Programación Web.

## Tecnologías utilizadas

| Tecnología            | Aplicación                                   |
| --------------------- | -------------------------------------------- |
| C# y .NET 10          | Desarrollo del sistema                       |
| ASP.NET Core MVC      | Arquitectura web                             |
| Razor                 | Renderización de vistas                      |
| Entity Framework Core | Acceso y persistencia de datos               |
| SQL Server            | Base de datos relacional                     |
| ASP.NET Core Identity | Autenticación y autorización                 |
| Bootstrap 5           | Interfaz responsive                          |
| JavaScript            | Interactividad y multimedia                  |
| HTML5 y CSS3          | Estructura y presentación                    |
| Ollama                | Integración local de inteligencia artificial |
| xUnit                 | Pruebas automatizadas                        |

## Arquitectura

La solución separa las responsabilidades en proyectos:

- **MiCaminoBJJ.Web:** controladores MVC, vistas Razor, servicios web y configuración de la aplicación.
- **MiCaminoBJJ.Core:** entidades y reglas de negocio.
- **MiCaminoBJJ.Infrastructure:** persistencia de datos e integración con Entity Framework Core.
- **MiCaminoBJJ.Web.Tests:** pruebas automatizadas del módulo web.
- **MiCaminoBJJ.Tests:** pruebas del núcleo.
- **MiCaminoBJJ:** aplicación móvil MAUI Blazor Hybrid desarrollada como proyecto complementario.

Esta separación facilita el mantenimiento, las pruebas y la evolución del sistema.

## Funcionalidades de la aplicación web

### Autenticación y perfiles

- Registro e inicio de sesión mediante ASP.NET Core Identity.
- Autorización por roles: Administrador y Practicante.
- Edición de nombre y cinturón.
- Carga y actualización de fotografías de perfil.
- Administración de preferencias del usuario.

### Biblioteca de técnicas

- Consulta de técnicas de Brazilian Jiu-Jitsu.
- Búsqueda normalizada por nombre y alias.
- Filtros combinables por cinturón, categoría y posición.
- Fichas educativas con descripción, objetivo, pasos y consejos.
- Recursos multimedia asociados a las técnicas.
- Reproductor integrado de YouTube.
- Recorrido educativo guiado para cinturón blanco.

### Internacionalización

La interfaz permite seleccionar:

- Español.
- Inglés.
- Portugués.

La implementación utiliza los mecanismos de localización de ASP.NET Core, archivos `.resx` y persistencia de la preferencia de idioma.

**Alcance:** las traducciones cubren la interfaz; los contenidos educativos almacenados en la base de datos permanecen en su idioma original.

### Guía del Practicante con inteligencia artificial

Integra un asistente que ayuda a consultar información relacionada con Brazilian Jiu-Jitsu.

La implementación local utiliza Ollama mediante una API compatible con el formato de chat de OpenAI.

Cuando el servicio de IA no está disponible, la aplicación dispone de un modo alternativo de consulta basado en el catálogo.

La ejecución de Ollama en desarrollo local no implica su disponibilidad automática en el hosting público.

### Administración

El rol Administrador dispone de funciones de gestión de técnicas y recursos multimedia, sujetas a políticas de autorización.

Las operaciones de auditoría y la generación de reportes PDF deben documentarse conforme al alcance efectivamente implementado y verificado en la versión entregada.

## Base de datos

La aplicación utiliza SQL Server y Entity Framework Core.

El modelo incluye entidades para usuarios, técnicas, posiciones, categorías, recursos multimedia, perfiles y preferencias, además de las tablas necesarias para ASP.NET Core Identity.

El esquema reportado durante la preparación del TFI contiene **21 tablas**, superando el mínimo académico de siete tablas adicionales a las de Identity.

## Seguridad

El sistema incorpora:

- ASP.NET Core Identity para gestión de usuarios.
- Contraseñas almacenadas mediante mecanismos de hashing de Identity.
- Políticas de autorización por rol.
- Protección antifalsificación en formularios POST.
- Validaciones del lado del servidor.
- Validación de tipos y contenido de archivos.
- Cabeceras de seguridad HTTP.
- Configuración sensible mediante User Secrets y variables de entorno.

Las contraseñas, claves de API y cadenas de conexión privadas no deben incorporarse al repositorio.

## Pruebas automatizadas

En la última ejecución reportada durante la preparación del TFI:

| Proyecto              | Pruebas aprobadas |
| --------------------- | ----------------: |
| MiCaminoBJJ.Tests     |               236 |
| MiCaminoBJJ.Web.Tests |               468 |
| **Total**             |           **704** |

Estos resultados corresponden a esa ejecución y deben actualizarse si se realizan modificaciones posteriores.

## Ejecución local

### Requisitos

- SDK de .NET 10.
- SQL Server o SQL Server LocalDB.
- Una base de datos configurada para la aplicación.
- Ollama, opcional para ejecutar el asistente con IA local.

### Comandos básicos

Desde la raíz de la solución:

```bash
dotnet restore
dotnet build MiCaminoBJJ.Web
dotnet run --project MiCaminoBJJ.Web --launch-profile http
```

La configuración de la conexión a SQL Server debe realizarse mediante los mecanismos seguros correspondientes al entorno.

## Despliegue

La aplicación web está preparada para su publicación en un hosting compatible con ASP.NET Core y SQL Server, como MonsterASP.NET.

**URL pública:** pendiente de incorporar tras verificar el despliegue.

## Acceso para evaluación académica

Se contemplan cuentas de demostración para los roles Administrador y Practicante.

Por seguridad, las contraseñas se entregan directamente al docente por un canal privado y no se publican en GitHub.

## Estado del proyecto

Proyecto desarrollado como Trabajo Final Integrador de Programación Web.

Las funcionalidades, pruebas y características indicadas corresponden al alcance de la versión documentada. Los módulos pendientes de implementación o verificación se identifican expresamente.

## Autoría

**Naiquen Anael Iturralde**

Tecnicatura Superior en Desarrollo de Software

Trabajo Final Integrador — Programación Web

**Mi Camino BJJ — Aprender, practicar y evolucionar.**
