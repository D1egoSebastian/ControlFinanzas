using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Debts.CreateDebtWithSchedule;
using ControlDeFinanzasIA.Application.UseCases.Debts.GetDebts;
using ControlDeFinanzasIA.Application.UseCases.Debts.GetDebtSchedule;
using ControlDeFinanzasIA.Application.UseCases.Debts.RegisterDebtPayment;
using ControlDeFinanzasIA.Application.UseCases.Debts.SkipDebtPayment;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// Screen for creating debts with a full installment plan, and for
/// tracking payments (register payment / skip installment) against an
/// existing debt's schedule.
/// </summary>
public sealed partial class DebtsViewModel : ViewModelBase
{
    private readonly GetDebtsQuery _getDebtsQuery;
    private readonly GetDebtScheduleQuery _getDebtScheduleQuery;
    private readonly CreateDebtWithScheduleUseCase _createUseCase;
    private readonly RegisterDebtPaymentUseCase _registerPaymentUseCase;
    private readonly SkipDebtPaymentUseCase _skipPaymentUseCase;
    private readonly ILogger<DebtsViewModel> _logger;

    public DebtsViewModel(
        GetDebtsQuery getDebtsQuery,
        GetDebtScheduleQuery getDebtScheduleQuery,
        CreateDebtWithScheduleUseCase createUseCase,
        RegisterDebtPaymentUseCase registerPaymentUseCase,
        SkipDebtPaymentUseCase skipPaymentUseCase,
        ILogger<DebtsViewModel> logger)
    {
        _getDebtsQuery = getDebtsQuery;
        _getDebtScheduleQuery = getDebtScheduleQuery;
        _createUseCase = createUseCase;
        _registerPaymentUseCase = registerPaymentUseCase;
        _skipPaymentUseCase = skipPaymentUseCase;
        _logger = logger;
        StartDate = DateTime.Today;
        Frequencies = [nameof(DebtFrequency.Monthly), nameof(DebtFrequency.Biweekly)];
        Currencies = Enum.GetValues<Currency>();
        _ = LoadDebtsAsync();
    }

    public ObservableCollection<Debt> Debts { get; } = [];
    public ObservableCollection<DebtPayment> Schedule { get; } = [];
    public IReadOnlyList<string> Frequencies { get; }
    public IReadOnlyList<Currency> Currencies { get; }

    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private decimal _originalAmount;
    [ObservableProperty] private Currency _currency = Currency.DOP;
    [ObservableProperty] private int _totalInstallments = 1;
    [ObservableProperty] private decimal _installmentAmount;
    [ObservableProperty] private string _frequency = nameof(DebtFrequency.Monthly);
    [ObservableProperty] private DateTime? _startDate;
    [ObservableProperty] private string? _counterpartyName;
    [ObservableProperty] private string? _note;
    [ObservableProperty] private string _formError = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _scheduleError = string.Empty;
    [ObservableProperty] private Debt? _selectedDebt;
    [ObservableProperty] private decimal _pendingBalance;

    partial void OnSelectedDebtChanged(Debt? value)
    {
        Schedule.Clear();
        PendingBalance = 0;
        if (value is not null)
        {
            _ = LoadScheduleAsync(value.Id);
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        FormError = string.Empty;
        if (StartDate is null)
        {
            FormError = "Selecciona la fecha de inicio.";
            return;
        }

        var command = new CreateDebtWithScheduleCommand(
            Description,
            OriginalAmount,
            Currency,
            TotalInstallments,
            InstallmentAmount,
            Frequency,
            DateOnly.FromDateTime(StartDate.Value),
            string.IsNullOrWhiteSpace(CounterpartyName) ? null : CounterpartyName,
            string.IsNullOrWhiteSpace(Note) ? null : Note);

        Result result = await _createUseCase.ExecuteAsync(command);
        if (!result.IsSuccess)
        {
            FormError = result.Error?.Message ?? "No se pudo crear la deuda.";
            return;
        }

        StatusMessage = "Deuda creada correctamente con su plan de cuotas.";
        ResetForm();
        await LoadDebtsAsync();
    }

    [RelayCommand]
    private async Task RegisterPaymentAsync(DebtPayment? payment)
    {
        if (payment is null)
        {
            return;
        }

        ScheduleError = string.Empty;
        Result result = await _registerPaymentUseCase.ExecuteAsync(payment.Id);
        if (!result.IsSuccess)
        {
            ScheduleError = result.Error?.Message ?? "No se pudo registrar el pago.";
            return;
        }

        StatusMessage = "Pago registrado correctamente.";
        await LoadDebtsAsync();
        if (SelectedDebt is not null)
        {
            await LoadScheduleAsync(SelectedDebt.Id);
        }
    }

    [RelayCommand]
    private async Task SkipPaymentAsync(DebtPayment? payment)
    {
        if (payment is null)
        {
            return;
        }

        ScheduleError = string.Empty;
        Result result = await _skipPaymentUseCase.ExecuteAsync(payment.Id);
        if (!result.IsSuccess)
        {
            ScheduleError = result.Error?.Message ?? "No se pudo saltar la cuota.";
            return;
        }

        StatusMessage = "Cuota saltada correctamente.";
        if (SelectedDebt is not null)
        {
            await LoadScheduleAsync(SelectedDebt.Id);
        }
    }

    private async Task LoadDebtsAsync()
    {
        try
        {
            Result result = await _getDebtsQuery.ExecuteAsync();
            if (!result.IsSuccess)
            {
                FormError = result.Error?.Message ?? "No se pudieron cargar las deudas.";
                return;
            }

            Debts.Clear();
            foreach (Debt debt in (IReadOnlyList<Debt>)result.Value!)
            {
                Debts.Add(debt);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load debts.");
            FormError = "No se pudieron cargar las deudas.";
        }
    }

    private async Task LoadScheduleAsync(Guid debtId)
    {
        try
        {
            Result result = await _getDebtScheduleQuery.ExecuteAsync(debtId);
            if (!result.IsSuccess)
            {
                ScheduleError = result.Error?.Message ?? "No se pudo cargar el calendario de cuotas.";
                return;
            }

            var payments = (IReadOnlyList<DebtPayment>)result.Value!;
            Schedule.Clear();
            foreach (DebtPayment payment in payments)
            {
                Schedule.Add(payment);
            }

            Debt? debt = Debts.FirstOrDefault(d => d.Id == debtId);
            PendingBalance = debt?.CalculatePendingBalance(payments).Amount ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load debt schedule for {DebtId}.", debtId);
            ScheduleError = "No se pudo cargar el calendario de cuotas.";
        }
    }

    private void ResetForm()
    {
        Description = string.Empty;
        OriginalAmount = 0;
        Currency = Currency.DOP;
        TotalInstallments = 1;
        InstallmentAmount = 0;
        Frequency = nameof(DebtFrequency.Monthly);
        StartDate = DateTime.Today;
        CounterpartyName = null;
        Note = null;
        FormError = string.Empty;
    }
}
