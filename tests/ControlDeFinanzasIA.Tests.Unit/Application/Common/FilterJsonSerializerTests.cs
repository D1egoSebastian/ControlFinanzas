using System.Text.Json;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Common;

/// <summary>
/// Unit tests for <see cref="FilterJsonSerializer"/>.
/// Uses pure xUnit assertions and hard-coded JSON strings for legacy
/// and future-version scenarios.
/// </summary>
public sealed class FilterJsonSerializerTests
{
    private static readonly Guid _categoryId = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _tag1 = new("22222222-2222-2222-2222-000000000001");
    private static readonly Guid _tag2 = new("22222222-2222-2222-2222-000000000002");

    [Fact]
    public void Serialize_Then_Deserialize_Round_Trip_PreservesAllProps()
    {
        ExpenseFilter original = new()
        {
            Page = 2,
            PageSize = 25,
            CategoryId = _categoryId,
            DateFrom = new DateOnly(2026, 1, 1),
            DateTo = new DateOnly(2026, 1, 31),
            TagIds = new[] { _tag1, _tag2 },
        };

        string json = FilterJsonSerializer.Serialize(original);
        ExpenseFilter roundTrip = FilterJsonSerializer.Deserialize<ExpenseFilter>(json);

        Assert.Equal(original.Page, roundTrip.Page);
        Assert.Equal(original.PageSize, roundTrip.PageSize);
        Assert.Equal(original.CategoryId, roundTrip.CategoryId);
        Assert.Equal(original.DateFrom, roundTrip.DateFrom);
        Assert.Equal(original.DateTo, roundTrip.DateTo);
        Assert.NotNull(roundTrip.TagIds);
        Assert.Equal(2, roundTrip.TagIds!.Count);
        Assert.Equal(_tag1, roundTrip.TagIds[0]);
        Assert.Equal(_tag2, roundTrip.TagIds[1]);

        string json2 = FilterJsonSerializer.Serialize(roundTrip);
        Assert.Equal(json, json2);
    }

    [Fact]
    public void Serialize_Then_Deserialize_RoundTrip_PreservesIncomeFilter()
    {
        IncomeFilter original = new()
        {
            Page = 1,
            PageSize = 50,
            CategoryId = null,
            DateFrom = null,
            DateTo = null,
            TagIds = null,
        };

        string json = FilterJsonSerializer.Serialize(original);
        IncomeFilter roundTrip = FilterJsonSerializer.Deserialize<IncomeFilter>(json);

        Assert.Equal(original.Page, roundTrip.Page);
        Assert.Equal(original.PageSize, roundTrip.PageSize);
        Assert.Null(roundTrip.CategoryId);
        Assert.Null(roundTrip.DateFrom);
        Assert.Null(roundTrip.DateTo);
        Assert.Null(roundTrip.TagIds);

        string json2 = FilterJsonSerializer.Serialize(roundTrip);
        Assert.Equal(json, json2);
    }

    [Fact]
    public void Deserialize_WithUnknownSchemaVersion_ThrowsInvalidOperationException()
    {
        const string json =
            """{"schemaVersion":2,"type":"ExpenseFilter","data":{"page":1,"pageSize":50}}""";

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => FilterJsonSerializer.Deserialize<ExpenseFilter>(json));

        Assert.Contains("Schema version 2 is not supported", ex.Message);
        Assert.Contains("Current version: 1", ex.Message);
    }

    [Fact]
    public void Deserialize_WithLegacyEnvelopeWithoutSchemaVersion_DefaultsTo1()
    {
        const string json =
            """{"type":"ExpenseFilter","data":{"page":3,"pageSize":10,"categoryId":null,"dateFrom":null,"dateTo":null,"tagIds":null}}""";

        ExpenseFilter filter = FilterJsonSerializer.Deserialize<ExpenseFilter>(json);

        Assert.Equal(3, filter.Page);
        Assert.Equal(10, filter.PageSize);
        Assert.Null(filter.CategoryId);
        Assert.Null(filter.DateFrom);
        Assert.Null(filter.DateTo);
        Assert.Null(filter.TagIds);
    }

    [Fact]
    public void Deserialize_WithMalformedJson_ThrowsJsonException()
    {
        const string json = "not json {{";

        Assert.ThrowsAny<JsonException>(() => FilterJsonSerializer.Deserialize<ExpenseFilter>(json));
    }

    [Fact]
    public void Deserialize_WithTypeMismatch_ThrowsInvalidOperationException()
    {
        const string json =
            """{"schemaVersion":1,"type":"ExpenseFilter","data":{"page":1,"pageSize":50}}""";

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => FilterJsonSerializer.Deserialize<IncomeFilter>(json));

        Assert.Contains("Type mismatch", ex.Message);
        Assert.Contains("'ExpenseFilter'", ex.Message);
        Assert.Contains("'IncomeFilter'", ex.Message);
    }

    [Fact]
    public void Serialize_GeneratesCorrectEnvelopeShape()
    {
        ExpenseFilter filter = new()
        {
            Page = 1,
            PageSize = 50,
            CategoryId = _categoryId,
            DateFrom = new DateOnly(2026, 1, 1),
            DateTo = new DateOnly(2026, 1, 31),
            TagIds = new[] { _tag1 },
        };

        string json = FilterJsonSerializer.Serialize(filter);

        using var doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.True(root.TryGetProperty("schemaVersion", out JsonElement schemaVersion));
        Assert.Equal(1, schemaVersion.GetInt32());
        Assert.True(root.TryGetProperty("type", out JsonElement type));
        Assert.Equal("ExpenseFilter", type.GetString());
        Assert.True(root.TryGetProperty("data", out JsonElement data));
        Assert.Equal(JsonValueKind.Object, data.ValueKind);

        Assert.True(data.TryGetProperty("page", out _));
        Assert.True(data.TryGetProperty("pageSize", out _));
        Assert.True(data.TryGetProperty("categoryId", out _));
        Assert.True(data.TryGetProperty("dateFrom", out _));
        Assert.True(data.TryGetProperty("dateTo", out _));
        Assert.True(data.TryGetProperty("tagIds", out _));

        Assert.DoesNotContain('\n', json);
    }

    [Fact]
    public void Deserialize_NonGeneric_MatchesGenericOutcome()
    {
        ExpenseFilter original = new()
        {
            Page = 2,
            PageSize = 25,
            CategoryId = _categoryId,
            DateFrom = new DateOnly(2026, 2, 1),
            DateTo = new DateOnly(2026, 2, 28),
            TagIds = new[] { _tag1, _tag2 },
        };

        string json = FilterJsonSerializer.Serialize(original);
        ExpenseFilter generic = FilterJsonSerializer.Deserialize<ExpenseFilter>(json);
        object nonGeneric = FilterJsonSerializer.Deserialize(json, typeof(ExpenseFilter));

        ExpenseFilter fromNonGeneric = Assert.IsType<ExpenseFilter>(nonGeneric);
        Assert.Equal(generic.Page, fromNonGeneric.Page);
        Assert.Equal(generic.PageSize, fromNonGeneric.PageSize);
        Assert.Equal(generic.CategoryId, fromNonGeneric.CategoryId);
        Assert.Equal(generic.DateFrom, fromNonGeneric.DateFrom);
        Assert.Equal(generic.DateTo, fromNonGeneric.DateTo);
        Assert.Equal(generic.TagIds, fromNonGeneric.TagIds);
    }
}
