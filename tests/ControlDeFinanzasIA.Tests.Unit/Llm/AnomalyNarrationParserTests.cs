using ControlDeFinanzasIA.Application.Llm;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Llm;

/// <summary>
/// Tabla plana de <c>(respuesta cruda del modelo, expectedCount) → diccionario
/// ordinal → mensaje</c> para <see cref="AnomalyNarrationParser"/> (F-014,
/// slice 5, D-9, S-6). Duplicado deliberado del estilo de
/// <see cref="LlmCategoryAnswerParserTests"/> — el parser NO conoce
/// <c>AlertId</c> ni <c>AnomalyItemDto</c>, solo ordinales (D-9).
/// </summary>
public sealed class AnomalyNarrationParserTests
{
    [Fact]
    public void Parse_CleanJsonArray_ReturnsAllItems()
    {
        string raw = """[{"id":1,"message":"Gasto inusual"},{"id":2,"message":"Otro gasto"}]""";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 2);

        Assert.Equal(2, result.Count);
        Assert.Equal("Gasto inusual", result[1]);
        Assert.Equal("Otro gasto", result[2]);
    }

    [Fact]
    public void Parse_PartiallyMalformedBatch_ReturnsOnlyParsedItems()
    {
        // S-6: 5 anomalías enviadas, JSON válido para 3, contenido malformado
        // para una 4ta, y ninguna entrada para la 5ta → diccionario de 3
        // entradas; el llamador conserva la plantilla para las otras 2.
        string raw = """
            [
              {"id":1,"message":"Mensaje uno"},
              {"id":2,"message":"Mensaje dos"},
              {"id":3,"message":"Mensaje tres"},
              {"id":4,"message":123}
            ]
            """;

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 5);

        Assert.Equal(3, result.Count);
        Assert.Equal("Mensaje uno", result[1]);
        Assert.Equal("Mensaje dos", result[2]);
        Assert.Equal("Mensaje tres", result[3]);
        Assert.False(result.ContainsKey(4));
        Assert.False(result.ContainsKey(5));
    }

    [Fact]
    public void Parse_ItemsWrappedPayload_ReturnsItems()
    {
        string raw = """{"items":[{"id":1,"message":"Uno"},{"id":2,"message":"Dos"}]}""";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 2);

        Assert.Equal(2, result.Count);
        Assert.Equal("Uno", result[1]);
        Assert.Equal("Dos", result[2]);
    }

    [Fact]
    public void Parse_ItemsWrappedPayload_PropertyNameIsCaseInsensitive()
    {
        string raw = """{"Items":[{"id":1,"message":"Uno"}]}""";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 1);

        Assert.Single(result);
        Assert.Equal("Uno", result[1]);
    }

    [Fact]
    public void Parse_FencedJsonWithJsonTag_ReturnsItems()
    {
        string raw = "```json\n[{\"id\":1,\"message\":\"Uno\"}]\n```";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 1);

        Assert.Single(result);
        Assert.Equal("Uno", result[1]);
    }

    [Fact]
    public void Parse_BareFencedJson_ReturnsItems()
    {
        string raw = "```\n[{\"id\":1,\"message\":\"Uno\"}]\n```";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 1);

        Assert.Single(result);
        Assert.Equal("Uno", result[1]);
    }

    [Fact]
    public void Parse_JsonWithProsePreamble_ReturnsItems()
    {
        string raw = "Claro, aquí está el análisis: [{\"id\":1,\"message\":\"Uno\"}] espero que ayude";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 1);

        Assert.Single(result);
        Assert.Equal("Uno", result[1]);
    }

    [Fact]
    public void Parse_StringTypedIdThatParsesAsInt_IsAccepted()
    {
        string raw = """[{"id":"1","message":"Uno"}]""";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 1);

        Assert.Single(result);
        Assert.Equal("Uno", result[1]);
    }

    [Fact]
    public void Parse_DuplicateIds_FirstOccurrenceWins()
    {
        string raw = """[{"id":1,"message":"Primero"},{"id":1,"message":"Segundo"}]""";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 1);

        Assert.Single(result);
        Assert.Equal("Primero", result[1]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(100)]
    public void Parse_OutOfRangeId_IsIgnored(int outOfRangeId)
    {
        string raw = $$"""[{"id":{{outOfRangeId}},"message":"Fuera de rango"}]""";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 5);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MessageLongerThan280Chars_IsTruncatedWithEllipsis()
    {
        string longMessage = new string('a', 300);
        string raw = $$"""[{"id":1,"message":"{{longMessage}}"}]""";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 1);

        Assert.Single(result);
        Assert.Equal(280, result[1].Length);
        Assert.EndsWith("…", result[1], StringComparison.Ordinal);
        Assert.Equal(new string('a', 279) + "…", result[1]);
    }

    [Fact]
    public void Parse_Message280CharsExactly_IsNotTruncated()
    {
        string message = new string('a', 280);
        string raw = $$"""[{"id":1,"message":"{{message}}"}]""";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 1);

        Assert.Single(result);
        Assert.Equal(message, result[1]);
    }

    [Fact]
    public void Parse_MissingIdField_ItemIsSkipped()
    {
        string raw = """[{"message":"Sin id"}]""";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 1);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MissingMessageField_ItemIsSkipped()
    {
        string raw = """[{"id":1}]""";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 1);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_EmptyMessageAfterTrim_ItemIsSkipped()
    {
        string raw = """[{"id":1,"message":"   "}]""";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 1);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_IdWrongType_ItemIsSkipped()
    {
        string raw = """[{"id":true,"message":"Uno"}]""";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 1);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_IdStringThatIsNotAnInt_ItemIsSkipped()
    {
        string raw = """[{"id":"not-a-number","message":"Uno"}]""";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 1);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_RootIsObjectWithoutItemsProperty_ReturnsEmpty()
    {
        string raw = """{"id":1,"message":"Uno"}""";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 1);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_NonObjectArrayItem_IsSkipped()
    {
        string raw = """["not an object", {"id":1,"message":"Uno"}]""";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 1);

        Assert.Single(result);
        Assert.Equal("Uno", result[1]);
    }

    [Fact]
    public void Parse_EmptyArray_ReturnsEmpty()
    {
        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse("[]", expectedCount: 5);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_GarbageText_ReturnsEmpty()
    {
        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(
            "No puedo procesar esta solicitud en este momento.",
            expectedCount: 5);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n  ")]
    public void Parse_EmptyOrWhitespaceOrNullInput_ReturnsEmpty(string? raw)
    {
        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 5);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MalformedJson_ReturnsEmpty()
    {
        string raw = """[{"id":1,"message":"Uno"""; // sin cerrar

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 1);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_ExpectedCountZero_AnyIdIsOutOfRange()
    {
        string raw = """[{"id":1,"message":"Uno"}]""";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 0);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_ClosingBraceInsideStringValue_IsNotMistakenForObjectEnd()
    {
        // Mismo caso regresivo que LlmCategoryAnswerParser (D-9): el
        // escaneo balanceado debe ser consciente de cadenas/escapes.
        string raw = """[{"id":1,"message":"a} b"}]""";

        IReadOnlyDictionary<int, string> result = AnomalyNarrationParser.Parse(raw, expectedCount: 1);

        Assert.Single(result);
        Assert.Equal("a} b", result[1]);
    }
}
