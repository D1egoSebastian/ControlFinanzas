using System.Globalization;
using System.Text;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Llm;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Domain.Goals;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Goals.GenerateSavingsPlan;

/// <summary>
/// Genera una narracion de IA para el plan de ahorro de una meta (F-016,
/// R-2..R-12, D-1). Toma unicamente <see cref="Guid"/> y recalcula todo
/// desde los repositorios — nunca confia en una figura provista por la
/// ViewModel (D-5). Es success-shaped para toda causa relacionada con IA:
/// solo "meta no encontrada" produce <see cref="Result.Failure(string, string)"/>.
/// </summary>
/// <remarks>
/// Sellada. No recibe <c>ICurrencyConverter</c> (F-011 R-4): la moneda de la
/// meta nunca se convierte en este camino.
/// </remarks>
public sealed class GenerateSavingsPlanUseCase
{
    /// <summary>Cota superior de <c>MaxTokens</c>, independiente de <see cref="AppSettings.AiMaxTokensPerRequest"/> (D-11).</summary>
    public const int MaxTokensCeiling = 300;

    /// <summary>Cota superior de <c>Temperature</c>, independiente de <see cref="AppSettings.AiTemperature"/> (D-11).</summary>
    public const double TemperatureCeiling = 0.5d;

    /// <summary>Longitud maxima del nombre de la meta enviado al modelo (D-3, D-14).</summary>
    public const int MaxGoalNameChars = 80;

    /// <summary>Timeout duro de la llamada al proveedor, igual que Fase 13 (R-5).</summary>
    public static readonly TimeSpan HardTimeout = TimeSpan.FromSeconds(15);

    private readonly IGoalRepository _goals;
    private readonly IGoalContributionRepository _contributions;
    private readonly ISettingsStore _settingsStore;
    private readonly ILLMClientFactory _factory;
    private readonly IPromptProvider _prompts;
    private readonly IClock _clock;
    private readonly ILogger<GenerateSavingsPlanUseCase> _logger;

    /// <summary>Inicializa una nueva instancia del use case.</summary>
    public GenerateSavingsPlanUseCase(
        IGoalRepository goals,
        IGoalContributionRepository contributions,
        ISettingsStore settingsStore,
        ILLMClientFactory factory,
        IPromptProvider prompts,
        IClock clock,
        ILogger<GenerateSavingsPlanUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(goals);
        ArgumentNullException.ThrowIfNull(contributions);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _goals = goals;
        _contributions = contributions;
        _settingsStore = settingsStore;
        _factory = factory;
        _prompts = prompts;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la generacion de la narracion para la meta
    /// <paramref name="goalId"/>.
    /// </summary>
    /// <param name="goalId">Identificador de la meta.</param>
    /// <param name="ct">Token de cancelacion del caller.</param>
    /// <returns>
    /// <see cref="Result.Success(object)"/> con un
    /// <see cref="SavingsPlanNarrationDto"/> para toda causa relacionada con
    /// IA. Solo devuelve <see cref="Result.Failure(string, string)"/> con
    /// codigo <c>"GoalNotFound"</c> cuando la meta no existe — evaluado
    /// ANTES de leer la configuracion de IA (R-2), por lo que se reporta
    /// incluso cuando la IA no esta configurada.
    /// </returns>
    public async Task<Result> ExecuteAsync(Guid goalId, CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid();

        Goal? goal = await _goals.GetByIdAsync(goalId, ct).ConfigureAwait(false);
        if (goal is null)
        {
            return Result.Failure("GoalNotFound", $"La meta {goalId} no existe.");
        }

        AppSettings settings = await _settingsStore.GetAppSettingsAsync(ct).ConfigureAwait(false);

        LlmClientResolution resolution = _factory.Resolve(settings);
        if (resolution.Status != LlmConfigurationStatus.Configured || resolution.Client is null)
        {
            _logger.LogDebug(
                "Savings plan narration skipped: not configured. CorrelationId={CorrelationId} Status={Status}",
                correlationId,
                resolution.Status);
            return Result.Success(new SavingsPlanNarrationDto(SavingsPlanNarrationStatus.NotConfigured, null));
        }

        ILLMClient client = resolution.Client;

        IReadOnlyList<GoalContribution> contributions = await _contributions
            .GetByGoalIdAsync(goal.Id, ct)
            .ConfigureAwait(false);

        GoalProgress progress = goal.CalculateProgress(contributions);
        SavingsPlan plan = SavingsPlanCalculator.Calculate(
            goal.TargetAmount.Amount, progress.Contributed.Amount, goal.TargetDate, _clock.Today);

        // AlreadyMet takes precedence when a goal is both met and past its target date
        // (mirrors GoalRow.ShowAlreadyMetMessage / F-011 D-4).
        PlanState state = plan.AlreadyMet
            ? PlanState.AlreadyMet
            : plan.TargetDateInPast ? PlanState.TargetDateInPast : PlanState.Live;

        string locale = SupportedLocales.IsSupported(settings.Locale) ? settings.Locale : SupportedLocales.Default;
        var culture = CultureInfo.GetCultureInfo(locale);

        PlanFacts facts = BuildFacts(state, goal, progress, plan, culture);
        string name = SanitizeGoalName(goal.Name);

        string systemTemplate = _prompts.GetTemplate(PromptNames.GenerateSavingsPlanSystem, locale);
        string userTemplate = _prompts.GetTemplate(PromptNames.GenerateSavingsPlanUser, locale);

        string userPrompt = userTemplate
            .Replace("{{plan}}", facts.Block, StringComparison.Ordinal)
            .Replace("{{goal_name}}", name, StringComparison.Ordinal); // LAST: a name like "{{plan}}" is never expanded.

        var request = new LlmRequest
        {
            Messages =
            [
                new LlmMessage(LlmRole.System, systemTemplate),
                new LlmMessage(LlmRole.User, userPrompt),
            ],
            Model = settings.AiModel,
            Temperature = Math.Min(settings.AiTemperature, TemperatureCeiling),
            MaxTokens = Math.Min(settings.AiMaxTokensPerRequest, MaxTokensCeiling),
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["correlationId"] = correlationId.ToString("N"),
            },
        };

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(HardTimeout);

        try
        {
            LlmResponse response = await client.CompleteAsync(request, linked.Token).ConfigureAwait(false);

            string text = SavingsPlanNarrationGuard.Clean(response.Content);
            if (text.Length == 0)
            {
                _logger.LogDebug(
                    "Savings plan narration empty after cleanup. CorrelationId={CorrelationId} Provider={Provider}",
                    correlationId,
                    client.ProviderName);
                return Unavailable();
            }

            if (!SavingsPlanNarrationGuard.IsGrounded(text, [.. facts.Values, name]))
            {
                // R-12: never log the narration text or the goal name, only the correlation id and outcome.
                _logger.LogWarning(
                    "Savings plan narration rejected by numeric guard. CorrelationId={CorrelationId} Status={Status}",
                    correlationId,
                    SavingsPlanNarrationStatus.Unavailable);
                return Unavailable();
            }

            _logger.LogDebug(
                "Savings plan narration completed. CorrelationId={CorrelationId} Provider={Provider} State={State} Status={Status} NarrationLength={NarrationLength}",
                correlationId,
                client.ProviderName,
                state,
                SavingsPlanNarrationStatus.Generated,
                text.Length);

            return Result.Success(new SavingsPlanNarrationDto(SavingsPlanNarrationStatus.Generated, text));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The caller superseded this request (e.g. a reload). Reporting it as a
            // failure would light up "AI unavailable" on a perfectly healthy pipeline —
            // the caller discards this silently (mirrors CategorizeExpenseUseCase D-7).
            throw;
        }
        catch (LlmException ex)
        {
            _logger.LogWarning(
                ex,
                "Savings plan narration failed. CorrelationId={CorrelationId} Provider={Provider} Kind={Kind}",
                correlationId,
                client.ProviderName,
                ex.Kind);
            return Unavailable();
        }
        catch (Exception ex)
        {
            // Broad on purpose: no exception may reach the UI (R-4). Covers the linked
            // CTS timeout (15s) and any transport/JSON failure not translated to LlmException.
            _logger.LogWarning(
                ex,
                "Savings plan narration failed unexpectedly. CorrelationId={CorrelationId} Provider={Provider}",
                correlationId,
                client.ProviderName);
            return Unavailable();
        }

        static Result Unavailable() =>
            Result.Success(new SavingsPlanNarrationDto(SavingsPlanNarrationStatus.Unavailable, null));
    }

    /// <summary>Estado explicito del plan recalculado (R-3, D-1). AlreadyMet tiene precedencia sobre TargetDateInPast.</summary>
    private enum PlanState
    {
        Live,
        AlreadyMet,
        TargetDateInPast,
    }

    /// <summary>
    /// Bloque de texto y valores formateados enviados al modelo para el
    /// estado seleccionado (D-2). <see cref="Values"/> son exactamente los
    /// lados derechos formateados — las claves y el token de estado no
    /// contienen digitos, por lo que no aportan nada al conjunto permitido
    /// del guard.
    /// </summary>
    private sealed record PlanFacts(string Block, IReadOnlyList<string> Values);

    private static PlanFacts BuildFacts(PlanState state, Goal goal, GoalProgress progress, SavingsPlan plan, CultureInfo culture)
    {
        string targetAmount = FormatAmount(goal.TargetAmount.Amount, goal.Currency, culture);

        switch (state)
        {
            case PlanState.AlreadyMet:
                {
                    string contributedAmount = FormatAmount(progress.Contributed.Amount, goal.Currency, culture);
                    string block = string.Join(
                        '\n',
                        "state = ALREADY_MET",
                        $"target_amount = {targetAmount}",
                        $"contributed_amount = {contributedAmount}");
                    return new PlanFacts(block, [targetAmount, contributedAmount]);
                }

            case PlanState.TargetDateInPast:
                {
                    string remainingAmount = FormatAmount(progress.Remaining.Amount, goal.Currency, culture);
                    string targetDate = FormatDate(goal.TargetDate);
                    string block = string.Join(
                        '\n',
                        "state = TARGET_DATE_IN_PAST",
                        $"remaining_amount = {remainingAmount}",
                        $"target_date = {targetDate}");
                    return new PlanFacts(block, [remainingAmount, targetDate]);
                }

            default:
                {
                    string monthlyContribution = FormatAmount(plan.MonthlyContributionRequired, goal.Currency, culture);
                    string monthsRemaining = FormatMonths(plan.MonthsRemaining);
                    string remainingAmount = FormatAmount(progress.Remaining.Amount, goal.Currency, culture);
                    string block = string.Join(
                        '\n',
                        "state = LIVE",
                        $"monthly_contribution = {monthlyContribution}",
                        $"months_remaining = {monthsRemaining}",
                        $"remaining_amount = {remainingAmount}",
                        $"target_amount = {targetAmount}");
                    return new PlanFacts(block, [monthlyContribution, monthsRemaining, remainingAmount, targetAmount]);
                }
        }
    }

    private static string FormatAmount(decimal amount, Domain.Enums.Currency currency, CultureInfo culture) =>
        amount.ToString("N2", culture) + " " + currency;

    private static string FormatMonths(int months) => months.ToString(CultureInfo.InvariantCulture);

    private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Sanea el nombre de la meta antes de insertarlo en el prompt de
    /// usuario (D-3, D-14): reemplaza caracteres de control (incluyendo
    /// <c>\r</c>, <c>\n</c>, <c>\t</c>) por un espacio, elimina <c>"</c>,
    /// colapsa corridas de espacios en blanco, recorta y trunca a
    /// <see cref="MaxGoalNameChars"/> caracteres.
    /// </summary>
    private static string SanitizeGoalName(string name)
    {
        string safe = name ?? string.Empty;

        var withoutControlChars = new StringBuilder(safe.Length);
        foreach (char c in safe)
        {
            withoutControlChars.Append(char.IsControl(c) ? ' ' : c);
        }

        string withoutQuotes = withoutControlChars.ToString().Replace("\"", string.Empty, StringComparison.Ordinal);
        string collapsed = CollapseWhitespace(withoutQuotes);
        string trimmed = collapsed.Trim();

        return trimmed.Length > MaxGoalNameChars ? trimmed[..MaxGoalNameChars] : trimmed;
    }

    private static string CollapseWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        bool lastWasWhitespace = false;

        foreach (char c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasWhitespace)
                {
                    builder.Append(' ');
                }

                lastWasWhitespace = true;
            }
            else
            {
                builder.Append(c);
                lastWasWhitespace = false;
            }
        }

        return builder.ToString();
    }
}
