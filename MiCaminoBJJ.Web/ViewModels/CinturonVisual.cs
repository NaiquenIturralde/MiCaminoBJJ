using MiCaminoBJJ.Core.Services;

namespace MiCaminoBJJ.Web.ViewModels;

/// <summary>
/// Presentación visual de los cinturones de BJJ (biblioteca y registro).
///
/// El cinturón es un DATO DE PERFIL/DOMINIO: sirve para filtros,
/// personalización y experiencia del practicante; NUNCA define permisos ni
/// roles (los roles siguen siendo Practicante / Mestre / Administrador y el
/// registro público siempre crea Practicante).
///
/// Mapea el nombre normalizado del cinturón a su PNG transparente
/// (wwwroot/images/belts/{slug}.png) y a la clase CSS que aplica el glow del
/// color correspondiente. "Violeta" es el nombre canónico del dominio
/// (el cinturón morado, igual que en la app móvil); "Morado" y "Purpura"
/// se aceptan como sinónimos al normalizar.
/// </summary>
public static class CinturonVisual
{
    /// <summary>Carpeta virtual de las imágenes de cinturones (PNG sin fondo).</summary>
    public const string Carpeta = "~/images/belts/";

    /// <summary>Opción visual de cinturón: valor de dominio + recursos de presentación.</summary>
    public sealed record Opcion(string Valor, string Etiqueta, string Imagen, string Clase)
    {
        /// <summary>Slug ASCII del cinturón (blanco, azul, violeta, marron, negro) para ids/clave.</summary>
        public string Slug => Clase["mccj-belt--".Length..];
    }

    /// <summary>
    /// Presentación de un cinturón por nombre de dominio.
    /// Devuelve null si el nombre no corresponde a un cinturón conocido
    /// (la vista degrada a una tarjeta sin imagen en lugar de romper).
    /// </summary>
    public static Opcion? Para(string? nombre) =>
        TextNormalizer.Normalize(nombre) switch
        {
            "BLANCO" => new Opcion("Blanco", "Blanco", Carpeta + "blanco.png", "mccj-belt--blanco"),
            "AZUL" => new Opcion("Azul", "Azul", Carpeta + "azul.png", "mccj-belt--azul"),
            "VIOLETA" or "MORADO" or "PURPURA" => new Opcion("Violeta", "Violeta", Carpeta + "violeta.png", "mccj-belt--violeta"),
            "MARRON" => new Opcion("Marrón", "Marrón", Carpeta + "marron.png", "mccj-belt--marron"),
            "NEGRO" => new Opcion("Negro", "Negro", Carpeta + "negro.png", "mccj-belt--negro"),
            _ => null
        };

    /// <summary>Las cinco opciones del registro, en orden de progresión (Blanco → Negro).</summary>
    public static IReadOnlyList<Opcion> OpcionesRegistro { get; } = new[]
    {
        new Opcion("Blanco", "Blanco", Carpeta + "blanco.png", "mccj-belt--blanco"),
        new Opcion("Azul", "Azul", Carpeta + "azul.png", "mccj-belt--azul"),
        new Opcion("Violeta", "Violeta", Carpeta + "violeta.png", "mccj-belt--violeta"),
        new Opcion("Marrón", "Marrón", Carpeta + "marron.png", "mccj-belt--marron"),
        new Opcion("Negro", "Negro", Carpeta + "negro.png", "mccj-belt--negro")
    };
}
