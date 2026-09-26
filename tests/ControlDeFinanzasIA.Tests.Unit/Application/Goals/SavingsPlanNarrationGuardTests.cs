using ControlDeFinanzasIA.Application.UseCases.Goals.GenerateSavingsPlan;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Goals;

/// <summary>
/// Tabla plana para <see cref="SavingsPlanNarrationGuard"/> (F-016, slice S1,
/// R-8, R-10, D-4). Pura, sin DI: ejercita <c>Clean</c> y <c>IsGrounded</c>
/// directamente, sin pasar por el use case.
/// </summary>
public sealed class SavingsPlanNarrationGuardTests
{
    [Fact]
    public void Clean_FencedBlock_UnwrapsFenceLines()
    {
        string raw = "```\nEste es tu plan.\n```";

        string result = SavingsPlanNarrationGuard.Clean(raw);

        Assert.Equal("Este es tu plan.", result);
    }

    [Fact]
    public void Clean_ThreeOrMoreNewlines_CollapsesToTwo()
    {
        string raw = "Primera linea.\n\n\n\nSegunda linea.";

        string result = SavingsPlanNarrationGuard.Clean(raw);

        Assert.Equal("Primera linea.\n\nSegunda linea.", result);
    }

    [Fact]
    public void Clean_WhitespaceOnlyInput_ReturnsEmpty()
    {
        string result = SavingsPlanNarrationGuard.Clean("   \n\t  \n  ");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Clean_NullInput_ReturnsEmpty()
    {
        string result = SavingsPlanNarrationGuard.Clean(null);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Clean_InputLongerThan600Chars_TruncatesAtWhitespaceWithoutSplittingAFigure()
    {
        // La figura "12,345.67 DOP" cae justo a caballo del corte de 600
        // caracteres si se cortara a lo bruto; el corte con retroceso al
        // ultimo espacio en blanco debe dejarla completa o completamente
        // fuera, nunca partida.
        string filler = new string('a', 590);
        string raw = filler + " 12,345.67 DOP resto del texto que sobra bastante despues del limite.";

        string result = SavingsPlanNarrationGuard.Clean(raw);

        Assert.True(result.Length <= 600);
        Assert.EndsWith("…", result, StringComparison.Ordinal);
        Assert.DoesNotContain("12,3…", result, StringComparison.Ordinal);
        Assert.DoesNotContain("12,345.6…", result, StringComparison.Ordinal);
    }

    [Fact]
    public void IsGrounded_ExactCopyOfSuppliedValue_Passes()
    {
        bool result = SavingsPlanNarrationGuard.IsGrounded(
            "Ahorra 12,345.67 DOP cada mes.",
            ["12,345.67 DOP"]);

        Assert.True(result);
    }

    [Fact]
    public void IsGrounded_SeparatorAgnostic_DifferentGroupingOfSameDigits_Passes()
    {
        // "12.345,67" (agrupacion es-DO invertida) produce los mismos runs
        // {12, 345, 67} que "12,345.67" (agrupacion en-US) enviado al modelo.
        bool result = SavingsPlanNarrationGuard.IsGrounded(
            "Ahorra 12.345,67 cada mes.",
            ["12,345.67 DOP"]);

        Assert.True(result);
    }

    [Fact]
    public void IsGrounded_UngroupedConcatenationOfDigits_IsRejected()
    {
        // "12345.67" produce los runs {12345, 67}; "12345" no es un run de
        // ninguna cifra enviada ({12, 345, 67}), asi que se rechaza.
        bool result = SavingsPlanNarrationGuard.IsGrounded(
            "Ahorra 12345.67 cada mes.",
            ["12,345.67 DOP"]);

        Assert.False(result);
    }

    [Fact]
    public void IsGrounded_HarmlessUnsentNumber_IsRejected()
    {
        // Costo documentado y aceptado (D13): "3 pasos" no fue enviado.
        bool result = SavingsPlanNarrationGuard.IsGrounded(
            "Sigue estos 3 pasos para lograrlo.",
            ["12,345.67 DOP"]);

        Assert.False(result);
    }

    [Fact]
    public void IsGrounded_YearNotSentForState_IsRejected()
    {
        bool result = SavingsPlanNarrationGuard.IsGrounded(
            "Para junio de 2027 lo lograras.",
            ["12,345.67 DOP", "6"]);

        Assert.False(result);
    }

    [Fact]
    public void IsGrounded_LeadingZeroInReply_IsEquivalentToWithoutLeadingZero()
    {
        bool result = SavingsPlanNarrationGuard.IsGrounded(
            "Cerca del dia 05 lo lograras.",
            ["5"]);

        Assert.True(result);
    }

    [Fact]
    public void IsGrounded_GoalNameDigitRun_PassesWhenNameIsSupplied()
    {
        bool result = SavingsPlanNarrationGuard.IsGrounded(
            "Tu meta Carro 2027 va muy bien.",
            ["12,345.67 DOP", "Carro 2027"]);

        Assert.True(result);
    }

    [Theory]
    [InlineData("Ahorraste el 100² del monto.")]
    [InlineData("Falta la mitad ½ del camino.")]
    [InlineData("Llevas ３ meses ahorrando.")]
    public void IsGrounded_NonAsciiNumericCharacter_IsRejected(string narration)
    {
        bool result = SavingsPlanNarrationGuard.IsGrounded(narration, ["12,345.67 DOP"]);

        Assert.False(result);
    }

    [Fact]
    public void IsGrounded_NoDigitsInNarration_Passes()
    {
        bool result = SavingsPlanNarrationGuard.IsGrounded(
            "Vas por muy buen camino, sigue asi.",
            ["12,345.67 DOP"]);

        Assert.True(result);
    }
}
