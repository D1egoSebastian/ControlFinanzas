namespace ControlDeFinanzasIA.Domain.Goals;

/// <summary>
/// Resultado de <see cref="SavingsPlanCalculator.Calculate"/>.
/// </summary>
/// <param name="MonthsRemaining">
/// Meses restantes hasta la fecha objetivo, nunca negativo. Piso de
/// <see cref="SavingsPlanCalculator.MinMonthsRemaining"/> para una meta
/// viva (aun no alcanzada, fecha aun no vencida); exactamente 0 cuando
/// <paramref name="AlreadyMet"/> o <paramref name="TargetDateInPast"/>
/// son <see langword="true"/>.
/// </param>
/// <param name="MonthlyContributionRequired">
/// Aporte mensual requerido, redondeado HACIA ARRIBA (ceiling) a
/// <see cref="SavingsPlanCalculator.AmountDecimals"/> decimales: un
/// minimo prescrito nunca se redondea a la baja (R-7). Es 0 cuando
/// <paramref name="AlreadyMet"/> o <paramref name="TargetDateInPast"/>
/// son <see langword="true"/>.
/// </param>
/// <param name="AlreadyMet">
/// <see langword="true"/> si <c>currentAmount &gt;= targetAmount</c>.
/// Independiente de <paramref name="TargetDateInPast"/>: ambas banderas
/// pueden ser verdaderas a la vez.
/// </param>
/// <param name="TargetDateInPast">
/// <see langword="true"/> si la fecha objetivo ya paso. Independiente
/// de <paramref name="AlreadyMet"/>.
/// </param>
public readonly record struct SavingsPlan(
    int MonthsRemaining,
    decimal MonthlyContributionRequired,
    bool AlreadyMet,
    bool TargetDateInPast);

/// <summary>
/// Servicio de dominio puro (F-011, R-7) que calcula el plan de ahorro
/// mensual requerido para alcanzar una meta. No realiza I/O, no
/// resuelve dependencias via DI y no realiza conversion de moneda:
/// recibe montos ya normalizados a la misma moneda por el caller
/// (garantizado por la validacion de moneda de
/// <c>ContributeToGoalUseCase</c>, R-4).
/// </summary>
/// <remarks>
/// Clase estatica, mismo patron que <c>Domain.Budgets.BudgetEvaluator</c>.
/// </remarks>
public static class SavingsPlanCalculator
{
    /// <summary>
    /// Piso de meses: una meta que vence hoy o dentro del mes requiere
    /// el remanente completo en un solo periodo.
    /// </summary>
    public const int MinMonthsRemaining = 1;

    /// <summary>Decimales del aporte mensual requerido.</summary>
    public const int AmountDecimals = 2;

    /// <summary>
    /// Calcula el plan de ahorro: meses restantes y aporte mensual
    /// requerido, o las banderas explicitas <c>AlreadyMet</c> /
    /// <c>TargetDateInPast</c> cuando aplican. Nunca divide por cero y
    /// nunca retorna un conteo de meses negativo, incluyendo cuando
    /// <paramref name="targetDate"/> es igual a <paramref name="today"/>.
    /// </summary>
    /// <param name="targetAmount">Monto objetivo. Debe ser &gt; 0.</param>
    /// <param name="currentAmount">Acumulado, MISMA moneda que <paramref name="targetAmount"/>. Debe ser &gt;= 0.</param>
    /// <param name="targetDate">Fecha objetivo de la meta.</param>
    /// <param name="today">Fecha actual (inyectada por el caller para determinismo en tests).</param>
    /// <returns>Un <see cref="SavingsPlan"/> con el resultado del calculo.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se lanza si <paramref name="targetAmount"/> no es estrictamente
    /// positivo, o si <paramref name="currentAmount"/> es negativo.
    /// </exception>
    public static SavingsPlan Calculate(
        decimal targetAmount, decimal currentAmount, DateOnly targetDate, DateOnly today)
    {
        if (targetAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetAmount), targetAmount, "targetAmount must be strictly positive.");
        }

        if (currentAmount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentAmount), currentAmount, "currentAmount must be non-negative.");
        }

        bool alreadyMet = currentAmount >= targetAmount;
        bool targetDateInPast = targetDate < today;

        // Both flags are evaluated BEFORE any division. No division is reachable in this
        // return, by construction: neither branch needs a month count.
        if (alreadyMet || targetDateInPast)
        {
            return new SavingsPlan(0, 0m, alreadyMet, targetDateInPast);
        }

        int rawMonths = ((targetDate.Year - today.Year) * 12) + (targetDate.Month - today.Month);
        if (targetDate.Day < today.Day)
        {
            rawMonths--;
        }

        int months = Math.Max(MinMonthsRemaining, rawMonths);
        decimal perMonth = CeilingToDecimals((targetAmount - currentAmount) / months, AmountDecimals);
        return new SavingsPlan(months, perMonth, false, false);
    }

    /// <summary>
    /// Redondeo HACIA ARRIBA a <paramref name="decimals"/> decimales: un
    /// minimo prescrito nunca se redondea a la baja. Deliberadamente
    /// distinto de <c>MidpointRounding.AwayFromZero</c>
    /// (<c>BudgetEvaluator</c>'s convention) porque este valor
    /// prescribe un minimo, no reporta un hecho observado (R-7
    /// reconciliation note).
    /// </summary>
    private static decimal CeilingToDecimals(decimal value, int decimals)
    {
        decimal factor = 1m;
        for (int i = 0; i < decimals; i++)
        {
            factor *= 10m;
        }

        return Math.Ceiling(value * factor) / factor;
    }
}
