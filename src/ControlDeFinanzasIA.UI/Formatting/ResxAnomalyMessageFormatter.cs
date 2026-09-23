using System.Globalization;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.UI.Resources;

namespace ControlDeFinanzasIA.UI.Formatting;

/// <summary>
/// Implementacion UI del puerto <see cref="IAnomalyMessageFormatter"/>
/// (F-014, R-8 vs D-9, slice S7). Resuelve
/// <see cref="Strings.DashboardAnomalyMessageFormat"/> sobre
/// <see cref="CultureInfo.CurrentCulture"/> y reutiliza
/// <see cref="MoneyFormat.ApproximateDop"/> para las cifras -- el mismo
/// formateador "≈ {N2} DOP" que el resto del Dashboard. Pura: nunca
/// invoca un LLM ni realiza I/O (Application nunca contiene literales
/// de cara al usuario; el texto persistido sigue siendo deterministico).
/// </summary>
public sealed class ResxAnomalyMessageFormatter : IAnomalyMessageFormatter
{
    /// <inheritdoc />
    public string Format(AnomalyMessageFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        return string.Format(
            CultureInfo.CurrentCulture,
            Strings.DashboardAnomalyMessageFormat,
            facts.CategoryName,
            MoneyFormat.ApproximateDop(facts.AmountDop),
            facts.RatioToMedian.ToString("0.00", CultureInfo.CurrentCulture),
            MoneyFormat.ApproximateDop(facts.MedianDop));
    }
}
