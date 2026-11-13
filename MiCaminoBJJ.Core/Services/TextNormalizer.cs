using System.Globalization;
using System.Text;

namespace MiCaminoBJJ.Core.Services;

/// <summary>
/// Normalización textual reutilizable para búsquedas.
/// </summary>
/// <remarks>
/// <para>Estrategia: normalización en dos orillas.</para>
/// <list type="bullet">
/// <item><description>En base de datos: los textos se normalizan al persistir o en columnas
/// generadas (ver etapa 2.2), por lo que NO se carga el catálogo en memoria.</description></item>
/// <item><description>En el cliente: el término buscado se normaliza con este mismo
/// helper, garantizando que ambas orillas usen exactamente la misma regla.</description></item>
/// </list>
/// <para>Regla: Unicode FormD + eliminación de marcas diacríticas + mayúsculas +
/// espacios colapsados. Consecuencia deliberada: toda letra con tilde colapsa a su
/// forma base ("América" → "AMERICA", "años" → "ANOS", "niño" → "NINO",
/// "pingüino" → "PINGUINO"). Esto hace la búsqueda tolerante a tildes y a la ñ
/// tecleada sin tilde, a costa de no distinguir "año" de "ano" — aceptable en
/// búsqueda parcial sobre nombres de técnicas.</para>
/// <para>SQLite puede usar NOCASE en columnas COLLATE, pero NOCASE es ASCII-only:
/// distingue ñ/á. Normalizar en ambas orillas con esta misma regla es el enfoque
/// robusto; NOCASE solo no cubriría tildes.</para>
/// </remarks>
public static class TextNormalizer
{
    /// <summary>
    /// Devuelve el texto normalizado para comparación/búsqueda:
    /// sin marcas diacríticas, mayúsculas y con espacios simples.
    /// Maneja null, empty y whitespace como string.Empty.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var formD = text.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);

        foreach (var c in formD)
        {
            // Descarta las marcas diacríticas (U+0300–U+036F): acentos, diéresis, eñe combinante.
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            sb.Append(c);
        }

        return CollapseSpaces(sb.ToString().ToUpperInvariant());
    }

    /// <summary>
    /// Compara dos textos normalizados (null-safe). Dos textos vacíos se consideran iguales.
    /// </summary>
    public static bool AreEqual(string? left, string? right)
        => Normalize(left) == Normalize(right);

    /// <summary>
    /// Indica si el texto contiene el término, comparando ambas formas normalizadas.
    /// </summary>
    public static bool Contains(string? text, string? term)
    {
        var normalizedTerm = Normalize(term);
        if (normalizedTerm.Length == 0)
            return false;

        return Normalize(text).Contains(normalizedTerm, StringComparison.Ordinal);
    }

    private static string CollapseSpaces(string value)
    {
        var sb = new StringBuilder(value.Length);
        var previousWasSpace = false;

        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!previousWasSpace)
                    sb.Append(' ');
                previousWasSpace = true;
            }
            else
            {
                sb.Append(c);
                previousWasSpace = false;
            }
        }

        return sb.ToString().Trim();
    }
}
