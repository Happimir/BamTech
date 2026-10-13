using StargateAPI.Business.Domain;

namespace StargateAPI.Tests;

public class NameNormalizerTests
{
    [Theory]
    [InlineData("  Ada Lovelace ", "ADA LOVELACE")]
    [InlineData("o'brien", "O'BRIEN")]
    [InlineData("josé", "JOSÉ")]
    [InlineData("Mary  Ann", "MARY  ANN")]       // internal whitespace is kept
    [InlineData("Zoë", "ZOË")]
    public void Normalize_trims_and_uppercases_invariantly(string input, string expected)
    {
        Assert.Equal(expected, NameNormalizer.Normalize(input));
    }

    [Fact]
    public void Composed_and_decomposed_forms_produce_the_same_key()
    {
        var composed = "José";        // é as one code point
        var decomposed = "José";     // e + combining acute accent

        Assert.Equal(NameNormalizer.Normalize(composed), NameNormalizer.Normalize(decomposed));
    }

    [Fact]
    public void Accents_are_not_stripped()
    {
        Assert.NotEqual(NameNormalizer.Normalize("José"), NameNormalizer.Normalize("Jose"));
    }

    [Fact]
    public void Turkish_dotted_i_is_uppercased_invariantly_regardless_of_culture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            Assert.Equal("ISTANBUL", NameNormalizer.Normalize("istanbul"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalize_rejects_blank_names(string? input)
    {
        Assert.False(NameNormalizer.TryNormalize(input, out _, out var error));
        Assert.Equal("Name is required.", error);
    }

    [Fact]
    public void TryNormalize_bounds_the_trimmed_length()
    {
        Assert.True(NameNormalizer.TryNormalize(new string('a', 300), out _, out _));
        Assert.True(NameNormalizer.TryNormalize("  " + new string('a', 300) + "  ", out _, out _));
        Assert.False(NameNormalizer.TryNormalize(new string('a', 301), out _, out _));
    }

    [Fact]
    public void TryNormalize_rejects_invalid_utf16()
    {
        Assert.False(NameNormalizer.TryNormalize("bad\uD800name", out _, out var error));
        Assert.Equal("Name contains invalid characters.", error);
    }
}
