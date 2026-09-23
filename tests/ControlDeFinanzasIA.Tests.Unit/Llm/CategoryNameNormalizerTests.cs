using ControlDeFinanzasIA.Application.Llm;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Llm;

/// <summary>
/// Tests para <see cref="CategoryNameNormalizer"/> (F-013, slice 5a, D-6, S-9).
/// </summary>
public sealed class CategoryNameNormalizerTests
{
    [Fact]
    public void Fold_DropsDiacritics_SoAccentedAndUnaccentedFormsMatch() =>
        Assert.Equal(CategoryNameNormalizer.Fold("alimentacion"), CategoryNameNormalizer.Fold("Alimentación"));

    [Theory]
    [InlineData("  \"Comida\"  ", "Comida")]
    [InlineData("'Comida'", "Comida")]
    [InlineData("Comida.", "Comida")]
    [InlineData("Comida,", "Comida")]
    [InlineData("  Comida  ", "Comida")]
    public void Clean_StripsQuotesAndPunctuation(string raw, string expected) =>
        Assert.Equal(expected, CategoryNameNormalizer.Clean(raw));

    [Fact]
    public void Clean_CollapsesInternalWhitespace() =>
        Assert.Equal("Comida Rápida", CategoryNameNormalizer.Clean("Comida    Rápida"));

    [Fact]
    public void Fold_DoesNotCollapseDistinctNamesThatOnlyDifferByALetter() =>
        // "Casa" y "Caza" no comparten diacríticos: Fold no debe volverlas
        // iguales solo porque ambas pasan por el mismo pipeline de
        // normalización (D-6: solo se quitan diacríticos y se
        // mayusculiza-invariante, nunca se corrigen letras).
        Assert.NotEqual(CategoryNameNormalizer.Fold("Casa"), CategoryNameNormalizer.Fold("Caza"));

    [Fact]
    public void Fold_IsCaseInsensitive() =>
        Assert.Equal(CategoryNameNormalizer.Fold("COMIDA"), CategoryNameNormalizer.Fold("comida"));

    [Fact]
    public void Clean_IsCaseSensitive_UnlikeFold() =>
        Assert.NotEqual(CategoryNameNormalizer.Clean("Comida"), CategoryNameNormalizer.Clean("comida"));
}
