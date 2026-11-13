using FluentAssertions;
using MiCaminoBJJ.Core.Services;

namespace MiCaminoBJJ.Tests.Services;

public class TextNormalizerTests
{
    // ── Tildes / diacríticos ────────────────────────────────────────────────

    [Theory]
    [InlineData("americana", "AMERICANA")]
    [InlineData("América", "AMERICA")]
    [InlineData("AMÉRICA", "AMERICA")]
    [InlineData("llave de brazo", "LLAVE DE BRAZO")]
    [InlineData("llÁve De BrAzO", "LLAVE DE BRAZO")]
    [InlineData("años", "ANOS")]
    [InlineData("posición", "POSICION")]
    [InlineData("barrídO", "BARRIDO")]
    public void Normalize_AccentedText_RemovesDiacritics(string input, string expected)
    {
        TextNormalizer.Normalize(input).Should().Be(expected);
    }

    // ── Mayúsculas / minúsculas ─────────────────────────────────────────────

    [Theory]
    [InlineData("rear naked choke", "REAR NAKED CHOKE")]
    [InlineData("REAR NAKED CHOKE", "REAR NAKED CHOKE")]
    [InlineData("Rear Naked Choke", "REAR NAKED CHOKE")]
    [InlineData("rEaR nAkEd ChOkE", "REAR NAKED CHOKE")]
    public void Normalize_MixedCase_ReturnsUppercase(string input, string expected)
    {
        TextNormalizer.Normalize(input).Should().Be(expected);
    }

    // ── ñ ───────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("niño", "NINO")]
    [InlineData("NIÑO", "NINO")]
    [InlineData("nino", "NINO")]
    [InlineData("Ñandú", "NANDU")]
    public void Normalize_TildeN_CollapsesToBaseN(string input, string expected)
    {
        // Decisión deliberada: ñ colapsa a N (tolerancia de búsqueda).
        TextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_NTildeAndPlainN_AreEquivalent()
    {
        TextNormalizer.Normalize("niño").Should().Be(TextNormalizer.Normalize("nino"));
    }

    // ── Espacios ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("\t \n", "")]
    public void Normalize_NullOrWhitespace_ReturnsEmpty(string? input, string expected)
    {
        TextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("  kimura  ", "KIMURA")]
    [InlineData(" kimura   lock ", "KIMURA LOCK")]
    [InlineData("triángulo\n inverso", "TRIANGULO INVERSO")]
    public void Normalize_ExtraSpaces_CollapsesAndTrims(string input, string expected)
    {
        TextNormalizer.Normalize(input).Should().Be(expected);
    }

    // ── Caracteres especiales ───────────────────────────────────────────────

    [Fact]
    public void Normalize_SpecialCharacters_ArePreserved()
    {
        // Los caracteres ASCII especiales no tienen marcas y se conservan.
        TextNormalizer.Normalize("cross-collar (gi)").Should().Be("CROSS-COLLAR (GI)");
    }

    [Fact]
    public void Normalize_OnlyAccentedLetters_KeepsBaseLetters()
    {
        // Solo letras acentuadas: las marcas se van, las letras base quedan.
        TextNormalizer.Normalize("áéí").Should().Be("AEI");
    }

    [Fact]
    public void Normalize_Emoji_IsPreserved()
    {
        TextNormalizer.Normalize("🥋 kimura").Should().Be("🥋 KIMURA");
    }

    // ── AreEqual ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("América", "americana", false)]
    [InlineData("América", "AMERICA", true)]
    [InlineData("niño", "nino", true)]
    [InlineData(null, "", true)]
    [InlineData(null, null, true)]
    [InlineData("kimura", "  Kimura  ", true)]
    public void AreEqual_ComparesNormalizedForms(string? left, string? right, bool expected)
    {
        TextNormalizer.AreEqual(left, right).Should().Be(expected);
    }

    // ── Contains ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Ezequiel Paragua", "paragua", true)]
    [InlineData("Ezequiel Paragua", "PARAGUA ", true)]
    [InlineData("Llave americana", "AMERIC", true)]
    [InlineData("Llave americana", "triángulo", false)]
    [InlineData("Llave Americana", "amÉriCa", true)]
    public void Contains_MatchesNormalizedSubstring(string text, string term, bool expected)
    {
        TextNormalizer.Contains(text, term).Should().Be(expected);
    }

    [Theory]
    [InlineData("kimura", null)]
    [InlineData("kimura", "")]
    [InlineData("kimura", "   ")]
    public void Contains_EmptyTerm_NeverMatches(string text, string? term)
    {
        TextNormalizer.Contains(text, term).Should().BeFalse();
    }

    [Fact]
    public void Contains_EmptyTextWithTerm_NeverMatches()
    {
        TextNormalizer.Contains("", "kimura").Should().BeFalse();
    }
}
