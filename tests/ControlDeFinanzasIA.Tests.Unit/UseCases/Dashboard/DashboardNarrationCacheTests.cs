using ControlDeFinanzasIA.Application.UseCases.Dashboard.Common;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UseCases.Dashboard;

/// <summary>
/// Tests unitarios de <see cref="DashboardNarrationCache"/> (F-014, slice 5,
/// D-11). Cache de sesión, en memoria, solo de resultados exitosos — no
/// confundir con caché durable (R-9 lo prohíbe explícitamente).
/// </summary>
public sealed class DashboardNarrationCacheTests
{
    [Fact]
    public void TryGetValue_AfterSetWithGeneratedStatus_ReturnsStoredValueWithoutRecompute()
    {
        var cache = new DashboardNarrationCache();
        int computeCount = 0;
        const string key = "cache-key";

        string ComputeAndMaybeCache()
        {
            if (cache.TryGetValue(key, out string cached))
            {
                return cached;
            }

            computeCount++;
            string computed = "generated narrative";
            cache.Set(key, computed, NarrativeStatus.Generated);
            return computed;
        }

        string first = ComputeAndMaybeCache();
        string second = ComputeAndMaybeCache();

        Assert.Equal(1, computeCount);
        Assert.Equal("generated narrative", first);
        Assert.Equal("generated narrative", second);
    }

    [Fact]
    public void TryGetValue_NoPriorSet_ReturnsFalse()
    {
        var cache = new DashboardNarrationCache();

        bool found = cache.TryGetValue("missing-key", out string? value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Theory]
    [InlineData(NarrativeStatus.NotConfigured)]
    [InlineData(NarrativeStatus.Unavailable)]
    [InlineData(NarrativeStatus.Skipped)]
    public void Set_NonGeneratedStatus_NeverStoresTheFallback(NarrativeStatus status)
    {
        var cache = new DashboardNarrationCache();

        cache.Set("key", "fallback text", status);

        bool found = cache.TryGetValue("key", out string? value);
        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void Set_GeneratedStatus_StoresTheValue()
    {
        var cache = new DashboardNarrationCache();

        cache.Set("key", "narrative", NarrativeStatus.Generated);

        bool found = cache.TryGetValue("key", out string? value);
        Assert.True(found);
        Assert.Equal("narrative", value);
    }

    [Fact]
    public void BuildKey_DifferentLocale_ProducesDifferentKeys()
    {
        string keyEs = DashboardNarrationCache.BuildKey("prompt-name", "es-DO", "OpenAI", "gpt-4o", "rendered prompt");
        string keyEn = DashboardNarrationCache.BuildKey("prompt-name", "en", "OpenAI", "gpt-4o", "rendered prompt");

        Assert.NotEqual(keyEs, keyEn);
    }

    [Fact]
    public void BuildKey_DifferentProvider_ProducesDifferentKeys()
    {
        string keyOpenAi = DashboardNarrationCache.BuildKey("prompt-name", "es-DO", "OpenAI", "gpt-4o", "rendered prompt");
        string keyAnthropic = DashboardNarrationCache.BuildKey("prompt-name", "es-DO", "Anthropic", "gpt-4o", "rendered prompt");

        Assert.NotEqual(keyOpenAi, keyAnthropic);
    }

    [Fact]
    public void BuildKey_DifferentModel_ProducesDifferentKeys()
    {
        string keyModelA = DashboardNarrationCache.BuildKey("prompt-name", "es-DO", "OpenAI", "gpt-4o", "rendered prompt");
        string keyModelB = DashboardNarrationCache.BuildKey("prompt-name", "es-DO", "OpenAI", "gpt-4o-mini", "rendered prompt");

        Assert.NotEqual(keyModelA, keyModelB);
    }

    [Fact]
    public void BuildKey_SameInputs_ProducesTheSameKey()
    {
        string keyA = DashboardNarrationCache.BuildKey("prompt-name", "es-DO", "OpenAI", "gpt-4o", "rendered prompt");
        string keyB = DashboardNarrationCache.BuildKey("prompt-name", "es-DO", "OpenAI", "gpt-4o", "rendered prompt");

        Assert.Equal(keyA, keyB);
    }

    [Fact]
    public void Set_CapacityExceeded_ClearsWholeCacheBeforeStoringTheNewEntry()
    {
        var cache = new DashboardNarrationCache();

        for (int i = 0; i < DashboardNarrationCache.Capacity; i++)
        {
            cache.Set($"key-{i}", $"value-{i}", NarrativeStatus.Generated);
        }

        // At capacity: the next Set overflows and must Clear() the whole
        // cache before storing the new entry (D-11) — a session holds a
        // handful of months, so LRU bookkeeping would be ceremony.
        cache.Set("key-overflow", "value-overflow", NarrativeStatus.Generated);

        Assert.False(cache.TryGetValue("key-0", out string? _));
        Assert.False(cache.TryGetValue($"key-{DashboardNarrationCache.Capacity - 1}", out string? _));
        Assert.True(cache.TryGetValue("key-overflow", out string? overflowValue));
        Assert.Equal("value-overflow", overflowValue);
    }

    [Fact]
    public void TryGetValue_TypeMismatch_ReturnsFalse()
    {
        var cache = new DashboardNarrationCache();
        cache.Set("key", "string value", NarrativeStatus.Generated);

        bool found = cache.TryGetValue("key", out int _);

        Assert.False(found);
    }
}
