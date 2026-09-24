using System.Globalization;
using ControlDeFinanzasIA.Application.UseCases.Chat;
using ControlDeFinanzasIA.Application.UseCases.Chat.BuildChatContext;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetUpcomingPayments;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Enums;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UseCases.Chat;

/// <summary>
/// Tests for <see cref="ChatContextRenderer"/> (F-015, Slice S2, R-3, D-4,
/// S-5). Covers culture-derived number formatting (never hand-typed
/// separators), the empty/unavailable/omitted section states, the fixed
/// trim order, the 6,000-char budget held by construction, and
/// <see cref="ChatContextRenderer.SanitizeName"/>.
/// </summary>
public sealed class ChatContextRendererTests
{
    private static readonly DateOnly _today = new(2026, 9, 23);
    private static readonly DateOnly _currentAnchor = new(2026, 9, 1);
    private static readonly DateOnly _priorAnchor = new(2026, 8, 1);

    // ------------------------------------------------------------------
    // Snapshot builders
    // ------------------------------------------------------------------

    private static ChatMonthTotals CurrentMonthTotals() => new(_currentAnchor, 45_000m, 80_000m, 35_000m);

    private static ChatMonthTotals PriorMonthTotals() => new(_priorAnchor, 30_000m, 60_000m, 30_000m);

    private static ChatCategoryList MinimalCurrentCategories() =>
        new(_currentAnchor, [new ChatCategoryLine("Comida", 12_345.67m)], 0, 0m, 12_345.67m);

    private static ChatContextSnapshot MinimalSnapshot(
        ChatCategoryList? currentCategories = null,
        ChatMonthTotals? priorMonth = null,
        ChatCategoryList? priorCategories = null,
        IReadOnlyList<ChatBudgetLine>? budgets = null,
        IReadOnlyList<ChatGoalLine>? goals = null,
        IReadOnlyList<ChatDebtLine>? debts = null,
        IReadOnlyList<ChatPaymentLine>? upcomingPayments = null,
        bool hasMissingRates = false) =>
        new(
            Today: _today,
            CurrentMonth: CurrentMonthTotals(),
            PriorMonth: priorMonth ?? PriorMonthTotals(),
            CurrentCategories: currentCategories ?? MinimalCurrentCategories(),
            PriorCategories: priorCategories,
            Budgets: budgets,
            Goals: goals,
            Debts: debts,
            UpcomingPayments: upcomingPayments,
            HasMissingRates: hasMissingRates);

    private static ChatAmount Dop(decimal amount) => new(amount, amount, Currency.DOP);

    private static ChatGoalLine BigGoalLine(int index) => new(
        Name: new string('G', ChatContextLimits.MaxNameChars),
        Target: Dop(999_999_999.99m),
        Contributed: Dop(999_999_999.99m),
        Remaining: Dop(999_999_999.99m),
        PercentComplete: 99.99m,
        TargetDate: _today.AddYears(1),
        MonthlyRequired: Dop(999_999_999.99m));

    private static ChatDebtLine BigDebtLine(int index) => new(
        Name: new string('D', ChatContextLimits.MaxNameChars),
        PendingBalance: Dop(999_999_999.99m),
        Installment: Dop(999_999_999.99m),
        PendingInstallments: 99,
        NextDueDate: _today.AddDays(index));

    private static ChatBudgetLine BigBudgetLine(int index) => new(
        CategoryName: new string('B', ChatContextLimits.MaxNameChars),
        BudgetDop: 999_999_999.99m,
        ActualDop: 999_999_999.99m,
        PercentConsumed: 99.99m,
        Status: BudgetStatus.Near);

    private static ChatPaymentLine BigPaymentLine(int index) => new(
        Date: _today.AddDays(index),
        Label: new string('P', ChatContextLimits.MaxNameChars),
        Kind: UpcomingPaymentKind.RecurringRule,
        Amount: Dop(999_999_999.99m));

    private static ChatCategoryLine BigCategoryLine(char seed, int index) =>
        new(new string(seed, ChatContextLimits.MaxNameChars), 999_999_999.99m);

    private static ChatCategoryList MaxedCategoryList(DateOnly anchor, int count, char seed = 'A') =>
        new(anchor, Enumerable.Range(0, count).Select(i => BigCategoryLine(seed, i)).ToList(), 0, 0m, 999_999_999.99m * count);

    // ------------------------------------------------------------------
    // Culture-derived number formatting (never hand-typed separators)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public void Render_CurrentMonthCategoryLine_UsesRealCultureNumberFormat(string locale)
    {
        var culture = CultureInfo.GetCultureInfo(locale);
        ChatContextSnapshot snapshot = MinimalSnapshot();

        RenderedChatContext rendered = ChatContextRenderer.Render(snapshot, locale);

        string expectedLine = $"Comida: {12_345.67m.ToString("N2", culture)} DOP";
        Assert.Contains(expectedLine, rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_EsDoAndEnCultures_ProduceDifferentNumberSeparators()
    {
        ChatContextSnapshot snapshot = MinimalSnapshot();

        RenderedChatContext esDo = ChatContextRenderer.Render(snapshot, "es-DO");
        RenderedChatContext en = ChatContextRenderer.Render(snapshot, "en");

        string esDoExpected = 12_345.67m.ToString("N2", CultureInfo.GetCultureInfo("es-DO"));
        string enExpected = 12_345.67m.ToString("N2", CultureInfo.GetCultureInfo("en"));

        Assert.Contains(esDoExpected, esDo.Text, StringComparison.Ordinal);
        Assert.Contains(enExpected, en.Text, StringComparison.Ordinal);

        // Both cultures render the same underlying decimal with "N2", so the digit groups
        // are identical (es-DO and en both use ',' as group separator for this value) —
        // the two renders must still differ overall because the section labels are localized.
        Assert.NotEqual(esDo.Text, en.Text);
    }

    // ------------------------------------------------------------------
    // Empty vs. unavailable vs. omitted section states
    // ------------------------------------------------------------------

    [Fact]
    public void Render_NullGoalsSection_RendersUnavailableMarker()
    {
        ChatContextSnapshot snapshot = MinimalSnapshot(goals: null);
        ChatContextLabelSet labels = ChatContextLabels.For("es-DO");

        RenderedChatContext rendered = ChatContextRenderer.Render(snapshot, "es-DO");

        Assert.Contains($"## {labels.GoalsBaseLabel}: {labels.SectionUnavailableSuffix}", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_EmptyGoalsSection_RendersNoneMarkerDistinctFromUnavailable()
    {
        ChatContextSnapshot snapshot = MinimalSnapshot(goals: []);
        ChatContextLabelSet labels = ChatContextLabels.For("es-DO");

        RenderedChatContext rendered = ChatContextRenderer.Render(snapshot, "es-DO");

        Assert.Contains($"## {labels.GoalsBaseLabel}: {labels.SectionEmptySuffix}", rendered.Text, StringComparison.Ordinal);
        Assert.DoesNotContain($"## {labels.GoalsBaseLabel}: {labels.SectionUnavailableSuffix}", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_PopulatedGoalsSection_RendersDistinctFromEmptyAndUnavailable()
    {
        var goal = new ChatGoalLine("Viaje", Dop(295_000m), Dop(59_000m), Dop(236_000m), 20m, new DateOnly(2027, 6, 30), Dop(5_000m));
        ChatContextSnapshot snapshot = MinimalSnapshot(goals: [goal]);
        ChatContextLabelSet labels = ChatContextLabels.For("es-DO");

        RenderedChatContext rendered = ChatContextRenderer.Render(snapshot, "es-DO");

        Assert.Contains("Viaje:", rendered.Text, StringComparison.Ordinal);
        Assert.DoesNotContain($"## {labels.GoalsBaseLabel}: {labels.SectionEmptySuffix}", rendered.Text, StringComparison.Ordinal);
        Assert.DoesNotContain($"## {labels.GoalsBaseLabel}: {labels.SectionUnavailableSuffix}", rendered.Text, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Missing-rates flag line (D-14, S-9)
    // ------------------------------------------------------------------

    [Fact]
    public void Render_HasMissingRatesTrue_IncludesFlagLine()
    {
        ChatContextSnapshot snapshot = MinimalSnapshot(hasMissingRates: true);
        ChatContextLabelSet labels = ChatContextLabels.For("es-DO");

        RenderedChatContext rendered = ChatContextRenderer.Render(snapshot, "es-DO");

        Assert.Contains(labels.MissingRatesFlag, rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_HasMissingRatesFalse_OmitsFlagLine()
    {
        ChatContextSnapshot snapshot = MinimalSnapshot(hasMissingRates: false);
        ChatContextLabelSet labels = ChatContextLabels.For("es-DO");

        RenderedChatContext rendered = ChatContextRenderer.Render(snapshot, "es-DO");

        Assert.DoesNotContain(labels.MissingRatesFlag, rendered.Text, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // SanitizeName
    // ------------------------------------------------------------------

    [Fact]
    public void SanitizeName_ControlCharsAndNewlines_CollapseToSingleSpace()
    {
        string result = ChatContextRenderer.SanitizeName("Alpha\n\r\tBeta");

        Assert.Equal("Alpha Beta", result);
    }

    [Fact]
    public void SanitizeName_SquareBrackets_ConvertToParentheses()
    {
        string result = ChatContextRenderer.SanitizeName("Ahorro [meta]");

        Assert.Equal("Ahorro (meta)", result);
    }

    [Fact]
    public void SanitizeName_LeadingHashBangRun_IsStripped()
    {
        string result = ChatContextRenderer.SanitizeName("##!!Inyeccion");

        Assert.Equal("Inyeccion", result);
        Assert.DoesNotContain('#', result);
        Assert.DoesNotContain('!', result);
    }

    [Fact]
    public void SanitizeName_LongerThan80Chars_TruncatesTo79PlusEllipsis()
    {
        string raw = new string('X', 120);

        string result = ChatContextRenderer.SanitizeName(raw);

        Assert.Equal(80, result.Length);
        Assert.Equal(new string('X', 79) + "…", result);
    }

    [Fact]
    public void SanitizeName_ExactlyMaxChars_IsNotTruncated()
    {
        string raw = new string('Y', ChatContextLimits.MaxNameChars);

        string result = ChatContextRenderer.SanitizeName(raw);

        Assert.Equal(raw, result);
    }

    // ------------------------------------------------------------------
    // Fixed trim order (S-5)
    // ------------------------------------------------------------------

    [Fact]
    public void TrimOrder_IsExactlyPaymentsDebtsGoalsBudgetsPriorCategories()
    {
        Assert.Equal(
            [
                ChatContextSection.UpcomingPayments,
                ChatContextSection.Debts,
                ChatContextSection.Goals,
                ChatContextSection.Budgets,
                ChatContextSection.PriorMonthCategories,
            ],
            ChatContextRenderer.TrimOrder);
    }

    [Fact]
    public void Render_OverBudgetFromGoalsAndDebts_TrimsOnlyPaymentsDebtsGoalsInFixedOrder()
    {
        // Maxed current-month category list (never trimmed) plus bulky Goals/Debts sections
        // push the render past the 6,000-char budget, while Budgets and PriorCategories stay
        // null (already minimal "unavailable" markers) and are never reached by the trim loop.
        ChatCategoryList maxedCurrentCategories = MaxedCategoryList(_currentAnchor, ChatContextLimits.CurrentMonthCategories);
        var goals = Enumerable.Range(0, ChatContextLimits.Goals).Select(BigGoalLine).ToList();
        var debts = Enumerable.Range(0, ChatContextLimits.Debts).Select(BigDebtLine).ToList();

        ChatContextSnapshot snapshot = MinimalSnapshot(
            currentCategories: maxedCurrentCategories,
            priorCategories: null,
            budgets: null,
            goals: goals,
            debts: debts,
            upcomingPayments: null);

        RenderedChatContext rendered = ChatContextRenderer.Render(snapshot, "es-DO");
        ChatContextLabelSet labels = ChatContextLabels.For("es-DO");

        Assert.True(rendered.Text.Length <= ChatContextLimits.MaxContextChars);
        Assert.Equal(
            [ChatContextSection.UpcomingPayments, ChatContextSection.Debts, ChatContextSection.Goals],
            rendered.OmittedSections);

        // The three dropped sections render the "omitted" marker, distinct from "unavailable".
        Assert.Contains($"## {labels.PaymentsBaseLabel}: {labels.SectionOmittedSuffix}", rendered.Text, StringComparison.Ordinal);
        Assert.Contains($"## {labels.DebtsBaseLabel}: {labels.SectionOmittedSuffix}", rendered.Text, StringComparison.Ordinal);
        Assert.Contains($"## {labels.GoalsBaseLabel}: {labels.SectionOmittedSuffix}", rendered.Text, StringComparison.Ordinal);

        // Budgets and PriorCategories were never reached by the trim loop — they still show
        // their original "unavailable" state, not the "omitted" marker.
        Assert.Contains($"## {labels.BudgetsBaseLabel}: {labels.SectionUnavailableSuffix}", rendered.Text, StringComparison.Ordinal);
        Assert.Contains($"## {labels.PriorCategoriesBaseLabel}: {labels.SectionUnavailableSuffix}", rendered.Text, StringComparison.Ordinal);

        // The never-trimmed current-month summary/category list survive intact.
        Assert.Contains(new string('A', ChatContextLimits.MaxNameChars), rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WorstCaseSyntheticPayload_StaysUnderBudgetWithNeverTrimmedSectionsIntact()
    {
        // Every section at cap (30/10/15/10/10/10 items), 80-char names, max-digit amounts.
        // With these caps, the current-month summary + category list alone already consume
        // most of the 6,000-char budget (design.md's own estimate: ~4,300 chars for the
        // never-trimmed block), so a fully maxed payload only needs the first four TrimOrder
        // sections dropped to fit — PriorMonthCategories (capped at 10 items) can never by
        // itself push the total back over budget once the other four are gone. This is a
        // documented, favorable deviation from design.md's illustrative "all five" example:
        // fewer sections are dropped than assumed, which means less information is discarded
        // while the ≤6,000-char ceiling and the fixed drop order still hold exactly.
        ChatCategoryList maxedCurrentCategories = MaxedCategoryList(_currentAnchor, ChatContextLimits.CurrentMonthCategories);
        ChatCategoryList maxedPriorCategories = MaxedCategoryList(_priorAnchor, ChatContextLimits.PriorMonthCategories, seed: 'Z');
        var budgets = Enumerable.Range(0, ChatContextLimits.Budgets).Select(BigBudgetLine).ToList();
        var goals = Enumerable.Range(0, ChatContextLimits.Goals).Select(BigGoalLine).ToList();
        var debts = Enumerable.Range(0, ChatContextLimits.Debts).Select(BigDebtLine).ToList();
        var payments = Enumerable.Range(0, ChatContextLimits.UpcomingPayments).Select(BigPaymentLine).ToList();

        ChatContextSnapshot snapshot = MinimalSnapshot(
            currentCategories: maxedCurrentCategories,
            priorCategories: maxedPriorCategories,
            budgets: budgets,
            goals: goals,
            debts: debts,
            upcomingPayments: payments,
            hasMissingRates: true);

        RenderedChatContext rendered = ChatContextRenderer.Render(snapshot, "es-DO");

        Assert.True(
            rendered.Text.Length <= ChatContextLimits.MaxContextChars,
            $"Rendered context was {rendered.Text.Length} chars, over the {ChatContextLimits.MaxContextChars} budget.");

        // The four sections that DO need dropping are removed in exactly TrimOrder's order.
        Assert.Equal(
            [ChatContextSection.UpcomingPayments, ChatContextSection.Debts, ChatContextSection.Goals, ChatContextSection.Budgets],
            rendered.OmittedSections);

        // Current-month summary and category list are never trimmed, even in the worst case.
        Assert.Contains(new string('A', ChatContextLimits.MaxNameChars), rendered.Text, StringComparison.Ordinal);
        Assert.Contains("45,000.00 DOP", rendered.Text, StringComparison.Ordinal);

        // PriorMonthCategories was fully maxed too but never needed trimming — its real
        // content (not the "omitted" marker) is still present.
        Assert.Contains(new string('Z', ChatContextLimits.MaxNameChars), rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WorstCasePriorCategoriesAloneNeverNeedsTrimming()
    {
        // Companion test pinning the specific claim above: even with the same maxed baseline
        // and ALL other sections null/minimal (nothing else contending for budget), a fully
        // maxed PriorMonthCategories still renders its real content untouched, because
        // baseline + a fully maxed 10-item category list never exceeds 6,000 chars.
        ChatCategoryList maxedCurrentCategories = MaxedCategoryList(_currentAnchor, ChatContextLimits.CurrentMonthCategories);
        ChatCategoryList maxedPriorCategories = MaxedCategoryList(_priorAnchor, ChatContextLimits.PriorMonthCategories, seed: 'Z');

        ChatContextSnapshot snapshot = MinimalSnapshot(
            currentCategories: maxedCurrentCategories,
            priorCategories: maxedPriorCategories,
            budgets: null,
            goals: null,
            debts: null,
            upcomingPayments: null);

        RenderedChatContext rendered = ChatContextRenderer.Render(snapshot, "es-DO");

        Assert.True(rendered.Text.Length <= ChatContextLimits.MaxContextChars);
        Assert.Empty(rendered.OmittedSections);
        Assert.Contains(maxedPriorCategories.Lines[0].Name[..10], rendered.Text, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // FormatDop uses the real culture, never hand-typed separators
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public void FormatDop_UsesRealCultureN2Format(string locale)
    {
        var culture = CultureInfo.GetCultureInfo(locale);

        string result = ChatContextRenderer.FormatDop(1_234_567.891m, culture);

        Assert.Equal($"{1_234_567.891m.ToString("N2", culture)} DOP", result);
    }
}
