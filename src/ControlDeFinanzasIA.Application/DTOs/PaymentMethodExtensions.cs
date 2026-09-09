using ControlDeFinanzasIA.Domain.Catalog;

namespace ControlDeFinanzasIA.Application.DTOs;

/// <summary>
/// Mapping extensions from <see cref="PaymentMethod"/> domain entities to DTOs.
/// </summary>
public static class PaymentMethodExtensions
{
    /// <summary>
    /// Maps a <see cref="PaymentMethod"/> to a <see cref="PaymentMethodDto"/>.
    /// </summary>
    /// <param name="paymentMethod">The payment method to map.</param>
    /// <returns>A trimmed DTO with the payment method identity, name and system flag.</returns>
    public static PaymentMethodDto ToDto(this PaymentMethod paymentMethod)
        => new(paymentMethod.Id, paymentMethod.Name, paymentMethod.IsSystem);
}
