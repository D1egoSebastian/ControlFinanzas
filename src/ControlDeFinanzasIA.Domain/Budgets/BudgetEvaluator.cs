namespace ControlDeFinanzasIA.Domain.Budgets;

/// <summary>
/// Resultado de <see cref="BudgetEvaluator.Evaluate"/>: el porcentaje
/// consumido (redondeado) y el <see cref="BudgetStatus"/> derivado de
/// ese mismo porcentaje redondeado.
/// </summary>
/// <param name="PercentConsumed">
/// Porcentaje consumido, redondeado a <see cref="BudgetEvaluator.PercentDecimals"/>
/// decimales. Nunca clamp-eado: un presupuesto sobregirado reporta un
/// valor mayor a 100.
/// </param>
/// <param name="Status">
/// Estado derivado del mismo <paramref name="PercentConsumed"/> ya
/// redondeado, para que la UI nunca muestre un porcentaje y un estado
/// contradictorios.
/// </param>
public readonly record struct BudgetEvaluation(decimal PercentConsumed, BudgetStatus Status);

/// <summary>
/// Servicio de dominio puro (F-010, R-6) que evalua el porcentaje
/// consumido de un presupuesto y su <see cref="BudgetStatus"/>. No
/// realiza I/O, no resuelve dependencias via DI y no realiza
/// conversion de moneda: recibe el monto presupuestado y el gasto
/// real ya normalizados a la misma moneda por el caller.
/// </summary>
/// <remarks>
/// Clase estatica: se invoca una sola vez por categoria presupuestada
/// por carga, por lo que una instancia seria pura ceremonia. Vive en
/// <c>Domain</c> porque el porcentaje consumido y el estado
/// Ok/Near/Exceeded son semantica de negocio del presupuesto, no
/// orquestacion de Application.
/// </remarks>
public static class BudgetEvaluator
{
    /// <summary>
    /// Umbral (confirmado 2026-09-20): una categoria pasa a
    /// <see cref="BudgetStatus.Near"/> al consumir 80% o mas de su
    /// presupuesto.
    /// </summary>
    public const decimal NearThresholdPercent = 80m;

    /// <summary>
    /// Umbral: una categoria pasa a <see cref="BudgetStatus.Exceeded"/>
    /// al consumir 100% o mas de su presupuesto.
    /// </summary>
    public const decimal ExceededThresholdPercent = 100m;

    /// <summary>
    /// Cantidad de decimales usados al reportar (y al umbralizar) el
    /// porcentaje consumido.
    /// </summary>
    public const int PercentDecimals = 2;

    /// <summary>
    /// Evalua el porcentaje consumido y el <see cref="BudgetStatus"/>
    /// resultante. El umbral se aplica sobre el porcentaje YA
    /// redondeado, no sobre la razon cruda, para que la UI nunca
    /// muestre por ejemplo "80.00%" junto a un estado <c>Ok</c>.
    /// </summary>
    /// <param name="budgetAmount">
    /// Monto presupuestado, ya normalizado por el caller a la moneda
    /// de comparacion. Debe ser &gt; 0.
    /// </param>
    /// <param name="actualSpend">
    /// Gasto real en la MISMA moneda que <paramref name="budgetAmount"/>.
    /// Debe ser &gt;= 0.
    /// </param>
    /// <returns>
    /// Un <see cref="BudgetEvaluation"/> con el porcentaje consumido
    /// (nunca clamp-eado) y el estado derivado.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se lanza si <paramref name="budgetAmount"/> no es estrictamente
    /// positivo, o si <paramref name="actualSpend"/> es negativo.
    /// </exception>
    public static BudgetEvaluation Evaluate(decimal budgetAmount, decimal actualSpend)
    {
        if (budgetAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(budgetAmount), budgetAmount, "budgetAmount must be strictly positive.");
        }

        if (actualSpend < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actualSpend), actualSpend, "actualSpend must be non-negative.");
        }

        decimal percent = Math.Round(
            actualSpend / budgetAmount * 100m, PercentDecimals, MidpointRounding.AwayFromZero);

        BudgetStatus status =
            percent >= ExceededThresholdPercent ? BudgetStatus.Exceeded :
            percent >= NearThresholdPercent ? BudgetStatus.Near :
            BudgetStatus.Ok;

        return new BudgetEvaluation(percent, status);
    }
}
