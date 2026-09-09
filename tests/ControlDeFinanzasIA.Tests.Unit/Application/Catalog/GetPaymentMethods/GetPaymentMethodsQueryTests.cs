using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Application.UseCases.Catalog.GetPaymentMethods;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Catalog.GetPaymentMethods;

/// <summary>
/// Unit tests for <see cref="GetPaymentMethodsQuery"/>. Uses
/// <see cref="FakePaymentMethodRepository"/> to inject controlled
/// payment method catalogs and trigger exception paths.
/// </summary>
public sealed class GetPaymentMethodsQueryTests
{
    private static PaymentMethod CreateMethod(string name, bool isSystem = true)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsSystem = isSystem,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

    [Fact]
    public async Task ExecuteAsync_WithEmptyCatalog_ReturnsSuccessWithEmptyList()
    {
        var repo = new FakePaymentMethodRepository();
        var query = new GetPaymentMethodsQuery(repo, NullLogger<GetPaymentMethodsQuery>.Instance);

        Result result = await query.ExecuteAsync();

        Assert.True(result.IsSuccess);
        IReadOnlyList<PaymentMethodDto> dtos = Assert.IsAssignableFrom<IReadOnlyList<PaymentMethodDto>>(result.Value);
        Assert.Empty(dtos);
        Assert.Equal(1, repo.GetAllCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithMethods_MapsToDtos()
    {
        var repo = new FakePaymentMethodRepository
        {
            Methods = new[]
            {
                CreateMethod("Efectivo"),
                CreateMethod("Tarjeta de crédito"),
                CreateMethod("Personalizado", isSystem: false),
            },
        };
        var query = new GetPaymentMethodsQuery(repo, NullLogger<GetPaymentMethodsQuery>.Instance);

        Result result = await query.ExecuteAsync();

        Assert.True(result.IsSuccess);
        IReadOnlyList<PaymentMethodDto> dtos = Assert.IsAssignableFrom<IReadOnlyList<PaymentMethodDto>>(result.Value);
        Assert.Equal(3, dtos.Count);

        PaymentMethodDto efectivo = dtos.Single(d => d.Name == "Efectivo");
        Assert.True(efectivo.IsSystem);

        PaymentMethodDto custom = dtos.Single(d => d.Name == "Personalizado");
        Assert.False(custom.IsSystem);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesIdsFromRepository()
    {
        Guid id1 = new("11111111-1111-1111-1111-000000000001");
        Guid id2 = new("22222222-2222-2222-2222-000000000002");
        var repo = new FakePaymentMethodRepository
        {
            Methods = new[]
            {
                new PaymentMethod { Id = id1, Name = "A", IsSystem = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new PaymentMethod { Id = id2, Name = "B", IsSystem = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            },
        };
        var query = new GetPaymentMethodsQuery(repo, NullLogger<GetPaymentMethodsQuery>.Instance);

        Result result = await query.ExecuteAsync();

        IReadOnlyList<PaymentMethodDto> dtos = Assert.IsAssignableFrom<IReadOnlyList<PaymentMethodDto>>(result.Value);
        Assert.Equal(id1, dtos[0].Id);
        Assert.Equal(id2, dtos[1].Id);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_ReturnsFailureWithRepositoryErrorCode()
    {
        var repo = new FakePaymentMethodRepository();
        repo.SetThrowOnGetAll(new InvalidOperationException("DB down"));
        var query = new GetPaymentMethodsQuery(repo, NullLogger<GetPaymentMethodsQuery>.Instance);

        Result result = await query.ExecuteAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("RepositoryError", result.Error!.Code);
    }
}
