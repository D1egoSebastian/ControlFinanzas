using ControlDeFinanzasIA.Application.Llm;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Llm;

/// <summary>
/// Tabla plana de <c>(texto crudo del modelo) → (Kind, CategoryName)</c>
/// para <see cref="LlmCategoryAnswerParser"/> (F-013, slice 5a, D-5, S-9).
/// Sin fixtures: el parser no conoce categorías ni repositorios (D-5).
/// </summary>
public sealed class LlmCategoryAnswerParserTests
{
    [Fact]
    public void Parse_CleanJson_ReturnsName()
    {
        LlmCategoryAnswer answer = LlmCategoryAnswerParser.Parse("""{"category":"Alimentación"}""");

        Assert.Equal(LlmAnswerKind.Name, answer.Kind);
        Assert.Equal("Alimentación", answer.CategoryName);
    }

    [Fact]
    public void Parse_FencedJsonWithJsonTag_ReturnsName()
    {
        string raw = "```json\n{\"category\":\"Alimentación\"}\n```";

        LlmCategoryAnswer answer = LlmCategoryAnswerParser.Parse(raw);

        Assert.Equal(LlmAnswerKind.Name, answer.Kind);
        Assert.Equal("Alimentación", answer.CategoryName);
    }

    [Fact]
    public void Parse_BareFencedJson_ReturnsName()
    {
        string raw = "```\n{\"category\":\"Alimentación\"}\n```";

        LlmCategoryAnswer answer = LlmCategoryAnswerParser.Parse(raw);

        Assert.Equal(LlmAnswerKind.Name, answer.Kind);
        Assert.Equal("Alimentación", answer.CategoryName);
    }

    [Fact]
    public void Parse_JsonWithProsePreamble_ReturnsName()
    {
        string raw = "Claro, aquí está mi respuesta: {\"category\":\"Alimentación\"} espero que ayude";

        LlmCategoryAnswer answer = LlmCategoryAnswerParser.Parse(raw);

        Assert.Equal(LlmAnswerKind.Name, answer.Kind);
        Assert.Equal("Alimentación", answer.CategoryName);
    }

    [Theory]
    [InlineData("""{"categoria":"Alimentación"}""")]
    [InlineData("""{"name":"Alimentación"}""")]
    public void Parse_KeyAliases_ReturnsName(string raw)
    {
        LlmCategoryAnswer answer = LlmCategoryAnswerParser.Parse(raw);

        Assert.Equal(LlmAnswerKind.Name, answer.Kind);
        Assert.Equal("Alimentación", answer.CategoryName);
    }

    [Fact]
    public void Parse_ExplicitJsonNull_ReturnsExplicitNone()
    {
        LlmCategoryAnswer answer = LlmCategoryAnswerParser.Parse("""{"category":null}""");

        Assert.Equal(LlmAnswerKind.ExplicitNone, answer.Kind);
        Assert.Null(answer.CategoryName);
    }

    [Theory]
    [InlineData("""{"category":"null"}""")]
    [InlineData("""{"category":"none"}""")]
    [InlineData("""{"category":"ninguna"}""")]
    [InlineData("""{"category":"NULL"}""")]
    [InlineData("""{"category":"Ninguna"}""")]
    public void Parse_StringNoneTokensInJson_ReturnsExplicitNone(string raw)
    {
        LlmCategoryAnswer answer = LlmCategoryAnswerParser.Parse(raw);

        Assert.Equal(LlmAnswerKind.ExplicitNone, answer.Kind);
        Assert.Null(answer.CategoryName);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("none")]
    [InlineData("ninguna")]
    [InlineData("NINGUNA")]
    public void Parse_BareStringNoneTokens_ReturnsExplicitNone(string raw)
    {
        LlmCategoryAnswer answer = LlmCategoryAnswerParser.Parse(raw);

        Assert.Equal(LlmAnswerKind.ExplicitNone, answer.Kind);
        Assert.Null(answer.CategoryName);
    }

    [Fact]
    public void Parse_BareStringUnder60CharsSingleLine_ReturnsName()
    {
        LlmCategoryAnswer answer = LlmCategoryAnswerParser.Parse("Alimentación");

        Assert.Equal(LlmAnswerKind.Name, answer.Kind);
        Assert.Equal("Alimentación", answer.CategoryName);
    }

    [Fact]
    public void Parse_TruncatedJson_FallsBackToBareString()
    {
        // Falta la llave de cierre del objeto: el escaneo balanceado nunca
        // vuelve a profundidad 0, así que la extracción JSON falla y el
        // texto completo cae al paso 3 (bare-string).
        string raw = """{"category":"Comida""";

        LlmCategoryAnswer answer = LlmCategoryAnswerParser.Parse(raw);

        Assert.Equal(LlmAnswerKind.Name, answer.Kind);
        Assert.Equal(raw, answer.CategoryName);
    }

    [Fact]
    public void Parse_ProseParagraph_ReturnsUnparseable()
    {
        string raw = "No estoy seguro de cuál categoría corresponde a esta " +
            "descripción, podría ser varias cosas distintas dependiendo del contexto.";

        LlmCategoryAnswer answer = LlmCategoryAnswerParser.Parse(raw);

        Assert.Equal(LlmAnswerKind.Unparseable, answer.Kind);
        Assert.Null(answer.CategoryName);
    }

    [Fact]
    public void Parse_BareString61Chars_ReturnsUnparseable()
    {
        string raw = new string('a', 61);

        LlmCategoryAnswer answer = LlmCategoryAnswerParser.Parse(raw);

        Assert.Equal(LlmAnswerKind.Unparseable, answer.Kind);
        Assert.Null(answer.CategoryName);
    }

    [Fact]
    public void Parse_BareString60Chars_ReturnsName()
    {
        string raw = new string('a', 60);

        LlmCategoryAnswer answer = LlmCategoryAnswerParser.Parse(raw);

        Assert.Equal(LlmAnswerKind.Name, answer.Kind);
        Assert.Equal(raw, answer.CategoryName);
    }

    [Fact]
    public void Parse_MultiLineBareString_ReturnsUnparseable()
    {
        string raw = "Alimentación\nCasa";

        LlmCategoryAnswer answer = LlmCategoryAnswerParser.Parse(raw);

        Assert.Equal(LlmAnswerKind.Unparseable, answer.Kind);
        Assert.Null(answer.CategoryName);
    }

    [Fact]
    public void Parse_BalancedBraceRegression_ClosingBraceInsideStringValueIsNotMistakenForObjectEnd()
    {
        // D-5: un escaneo naive por IndexOf('{')/LastIndexOf('}') o un
        // contador de profundidad ciego a literales de cadena mal-cortaría
        // este texto en la primera "}" (dentro del valor), produciendo JSON
        // inválido. El escaneo consciente de cadenas/escapes debe encontrar
        // la "}" real de cierre, al final.
        string raw = """{"category":"a} b"}""";

        LlmCategoryAnswer answer = LlmCategoryAnswerParser.Parse(raw);

        Assert.Equal(LlmAnswerKind.Name, answer.Kind);
        Assert.Equal("a} b", answer.CategoryName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n  ")]
    public void Parse_EmptyOrWhitespaceOrNullInput_ReturnsUnparseable(string? raw)
    {
        LlmCategoryAnswer answer = LlmCategoryAnswerParser.Parse(raw);

        Assert.Equal(LlmAnswerKind.Unparseable, answer.Kind);
        Assert.Null(answer.CategoryName);
    }
}
