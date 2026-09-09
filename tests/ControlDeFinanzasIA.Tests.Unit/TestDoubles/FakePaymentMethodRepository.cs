using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Catalog;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="IPaymentMethodRepository"/> used in
/// unit tests. Configurable result for <see cref="GetAllAsync"/> and
/// a settable list of methods to return.
/// </summary>
public sealed class FakePaymentMethodRepository : IPaymentMethodRepository
{
    private readonly List<PaymentMethod> _methods = new();
    private Exception? _throwOnGetAll;

    /// <summary>
    /// Gets or sets the methods returned by <see cref="GetAllAsync"/>.
    /// When <c>null</c> the fake returns an empty list.
    /// </summary>
    public IReadOnlyList<PaymentMethod>? Methods
    {
        get => _methods;
        set
        {
            _methods.Clear();
            if (value is not null)
            {
                _methods.AddRange(value);
            }
        }
    }

    /// <summary>
    /// Gets the number of times <see cref="GetAllAsync"/> has been called.
    /// </summary>
    public int GetAllCallCount { get; private set; }

    /// <summary>
    /// Configures <see cref="GetAllAsync"/> to throw the given exception.
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    public void SetThrowOnGetAll(Exception exception) => _throwOnGetAll = exception;

    public Task<Result> AddAsync(PaymentMethod paymentMethod, CancellationToken ct = default)
    {
        _methods.Add(paymentMethod);
        return Task.FromResult(Result.Success(null!));
    }

    public Task<Result> UpdateAsync(PaymentMethod paymentMethod, CancellationToken ct = default)
        => Task.FromResult(Result.Success(null!));

    public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        _methods.RemoveAll(m => m.Id == id);
        return Task.FromResult(Result.Success(null!));
    }

    public Task<PaymentMethod?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult<PaymentMethod?>(_methods.FirstOrDefault(m => m.Id == id));

    public Task<IReadOnlyList<PaymentMethod>> GetAllAsync(CancellationToken ct = default)
    {
        GetAllCallCount++;
        if (_throwOnGetAll is not null)
        {
            return Task.FromException<IReadOnlyList<PaymentMethod>>(_throwOnGetAll);
        }
        return Task.FromResult<IReadOnlyList<PaymentMethod>>(_methods.ToList());
    }
}
