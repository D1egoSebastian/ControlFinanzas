using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Recurring.CreateFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.CreateSubscription;
using ControlDeFinanzasIA.Application.UseCases.Recurring.DeactivateRecurringRule;
using ControlDeFinanzasIA.Application.UseCases.Recurring.DeleteRecurringRule;
using ControlDeFinanzasIA.Application.UseCases.Recurring.GenerateDueRecurring;
using ControlDeFinanzasIA.Application.UseCases.Recurring.SkipNextOccurrence;
using ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateSubscription;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// Demo screen for creating and updating subscription rules.
/// </summary>
public sealed partial class RecurringRulesViewModel : ViewModelBase
{
    private readonly IRecurringRuleRepository _repository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IPaymentMethodRepository _paymentMethodRepository;
    private readonly CreateSubscriptionUseCase _createUseCase;
    private readonly UpdateSubscriptionUseCase _updateUseCase;
    private readonly CreateFixedExpenseUseCase _createFixedExpenseUseCase;
    private readonly UpdateFixedExpenseUseCase _updateFixedExpenseUseCase;
    private readonly GenerateDueRecurringUseCase _generateUseCase;
    private readonly DeactivateRecurringRuleUseCase _deactivateUseCase;
    private readonly DeleteRecurringRuleUseCase _deleteUseCase;
    private readonly SkipNextOccurrenceUseCase _skipUseCase;
    private readonly ILogger<RecurringRulesViewModel> _logger;

    public RecurringRulesViewModel(
        IRecurringRuleRepository repository,
        ICategoryRepository categoryRepository,
        IPaymentMethodRepository paymentMethodRepository,
        CreateSubscriptionUseCase createUseCase,
        UpdateSubscriptionUseCase updateUseCase,
        CreateFixedExpenseUseCase createFixedExpenseUseCase,
        UpdateFixedExpenseUseCase updateFixedExpenseUseCase,
        GenerateDueRecurringUseCase generateUseCase,
        DeactivateRecurringRuleUseCase deactivateUseCase,
        DeleteRecurringRuleUseCase deleteUseCase,
        SkipNextOccurrenceUseCase skipUseCase,
        ILogger<RecurringRulesViewModel> logger)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _paymentMethodRepository = paymentMethodRepository;
        _createUseCase = createUseCase;
        _updateUseCase = updateUseCase;
        _createFixedExpenseUseCase = createFixedExpenseUseCase;
        _updateFixedExpenseUseCase = updateFixedExpenseUseCase;
        _generateUseCase = generateUseCase;
        _deactivateUseCase = deactivateUseCase;
        _deleteUseCase = deleteUseCase;
        _skipUseCase = skipUseCase;
        _logger = logger;
        StartDate = DateTime.Today;
        Frequencies = ["Monthly", "Bimonthly", "Yearly"];
        Currencies = Enum.GetValues<Currency>();
        _ = LoadAsync();
    }

    public ObservableCollection<RecurringRule> Rules { get; } = [];
    public IReadOnlyList<Category> Categories { get; private set; } = [];
    public IReadOnlyList<PaymentMethod> PaymentMethods { get; private set; } = [];
    public IReadOnlyList<string> Frequencies { get; }
    public IReadOnlyList<string> RuleTypes { get; } = ["Subscription", "FixedExpense"];
    public IReadOnlyList<Currency> Currencies { get; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private Currency _currency = Currency.DOP;
    [ObservableProperty] private string _frequency = "Monthly";
    [ObservableProperty] private string _ruleType = "Subscription";
    [ObservableProperty] private DateTime? _startDate;
    [ObservableProperty] private DateTime? _endDate;
    [ObservableProperty] private Category? _selectedCategory;
    [ObservableProperty] private PaymentMethod? _selectedPaymentMethod;
    [ObservableProperty] private RecurringRule? _selectedRule;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _formError = string.Empty;
    [ObservableProperty] private string _generationError = string.Empty;
    [ObservableProperty] private bool _isGenerating;

    public ObservableCollection<RecurringGenerationPreview> PreviewItems { get; } = [];

    public bool IsEditing => SelectedRule is not null;

    public static bool IsEditableRule(RecurringRule rule) => rule is Subscription or FixedExpense;

    partial void OnSelectedRuleChanged(RecurringRule? value)
    {
        if (value is null)
        {
            ResetForm();
        }
        else
        {
            RuleType = value is FixedExpense ? "FixedExpense" : "Subscription";
            Name = value.Name;
            Amount = value.Amount.Amount;
            Currency = value.Currency;
            Frequency = value.Frequency;
            StartDate = value.StartDate.ToDateTime(TimeOnly.MinValue);
            EndDate = value.EndDate?.ToDateTime(TimeOnly.MinValue);
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == value.CategoryId);
            SelectedPaymentMethod = PaymentMethods.FirstOrDefault(p => p.Id == value.PaymentMethodId);
        }

        OnPropertyChanged(nameof(IsEditing));
        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        FormError = string.Empty;
        if (SelectedCategory is null || SelectedPaymentMethod is null || StartDate is null)
        {
            FormError = "Selecciona categoría, método de pago y fecha de inicio.";
            return;
        }

        var start = DateOnly.FromDateTime(StartDate.Value);
        DateOnly? end = EndDate.HasValue ? DateOnly.FromDateTime(EndDate.Value) : null;
        Result result = SelectedRule is null && RuleType == "Subscription"
            ? await _createUseCase.ExecuteAsync(new CreateSubscriptionCommand(
                Name, Amount, Currency, Frequency, start, end,
                SelectedCategory.Id, SelectedPaymentMethod.Id))
            : SelectedRule is null
            ? await _createFixedExpenseUseCase.ExecuteAsync(new CreateFixedExpenseCommand(
                Name, Amount, Currency, Frequency, start, end,
                SelectedCategory.Id, SelectedPaymentMethod.Id))
            : RuleType == "Subscription"
            ? await _updateUseCase.ExecuteAsync(new UpdateSubscriptionCommand(
                SelectedRule.Id, Name, Amount, Currency, Frequency, start, end,
                SelectedCategory.Id, SelectedPaymentMethod.Id))
            : await _updateFixedExpenseUseCase.ExecuteAsync(new UpdateFixedExpenseCommand(
                SelectedRule.Id, Name, Amount, Currency, Frequency, start, end,
                SelectedCategory.Id, SelectedPaymentMethod.Id));

        if (!result.IsSuccess)
        {
            FormError = result.Error?.Message ?? "No se pudo guardar la regla recurrente.";
            return;
        }

        StatusMessage = SelectedRule is null
            ? "Regla recurrente creada correctamente."
            : "Regla recurrente actualizada correctamente.";
        await LoadRulesAsync();
        SelectedRule = null;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        SelectedRule = null;
        FormError = string.Empty;
    }

    [RelayCommand]
    private void Edit(RecurringRule? rule)
    {
        if (rule is not null && IsEditableRule(rule))
        {
            SelectedRule = rule;
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            Categories = await _categoryRepository.GetAllAsync();
            PaymentMethods = await _paymentMethodRepository.GetAllAsync();
            OnPropertyChanged(nameof(Categories));
            OnPropertyChanged(nameof(PaymentMethods));
            await LoadRulesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load recurring subscriptions.");
            FormError = "No se pudieron cargar las reglas recurrentes.";
        }
    }

    private async Task LoadRulesAsync()
    {
        IReadOnlyList<RecurringRule> rules = await _repository.GetAllAsync();
        Rules.Clear();
        foreach (RecurringRule rule in rules)
        {
            Rules.Add(rule);
        }
    }

    private void ResetForm()
    {
        Name = string.Empty;
        Amount = 0;
        Currency = Currency.DOP;
        Frequency = "Monthly";
        RuleType = "Subscription";
        StartDate = DateTime.Today;
        EndDate = null;
        FormError = string.Empty;
    }

    [RelayCommand]
    private async Task PreviewGenerationAsync()
    {
        IsGenerating = true;
        GenerationError = string.Empty;
        try
        {
            Result result = await _generateUseCase.PreviewAsync();
            if (!result.IsSuccess)
            {
                GenerationError = result.Error?.Message ?? "No se pudo calcular la vista previa.";
                return;
            }
            PreviewItems.Clear();
            foreach (RecurringGenerationPreview item in (IReadOnlyList<RecurringGenerationPreview>)result.Value!)
            {
                PreviewItems.Add(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to preview recurring generation.");
            GenerationError = "No se pudo calcular la vista previa.";
        }
        finally { IsGenerating = false; }
    }

    [RelayCommand]
    private async Task ConfirmGenerationAsync()
    {
        IsGenerating = true;
        GenerationError = string.Empty;
        try
        {
            Result result = await _generateUseCase.ExecuteAsync();
            if (!result.IsSuccess)
            {
                GenerationError = result.Error?.Message ?? "No se pudieron generar los movimientos.";
            }
            else
            {
                StatusMessage = $"Se generaron {result.Value} movimientos.";
            }

            PreviewItems.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to confirm recurring generation.");
            GenerationError = "No se pudieron generar los movimientos.";
        }
        finally { IsGenerating = false; }
    }

    [RelayCommand]
    private async Task DeactivateSelectedAsync()
    {
        if (SelectedRule is null)
        {
            return;
        }

        Result result = await _deactivateUseCase.ExecuteAsync(SelectedRule.Id);
        if (!result.IsSuccess) { FormError = result.Error?.Message ?? "No se pudo desactivar la regla."; return; }
        StatusMessage = "Regla desactivada correctamente.";
        await LoadRulesAsync();
    }

    [RelayCommand]
    private async Task SkipSelectedAsync()
    {
        if (SelectedRule is null)
        {
            return;
        }

        Result result = await _skipUseCase.ExecuteAsync(SelectedRule.Id);
        if (!result.IsSuccess) { FormError = result.Error?.Message ?? "No se pudo saltar la ocurrencia."; return; }
        StatusMessage = "Ocurrencia saltada correctamente.";
        await LoadRulesAsync();
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedRule is null)
        {
            return;
        }

        Result result = await _deleteUseCase.ExecuteAsync(SelectedRule.Id);
        if (!result.IsSuccess) { FormError = result.Error?.Message ?? "No se pudo eliminar la regla."; return; }
        StatusMessage = "Regla eliminada correctamente.";
        SelectedRule = null;
        await LoadRulesAsync();
    }
}
