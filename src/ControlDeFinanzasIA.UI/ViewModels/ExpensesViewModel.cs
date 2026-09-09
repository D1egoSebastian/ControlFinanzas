using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Application.UseCases.Catalog.CreateTag;
using ControlDeFinanzasIA.Application.UseCases.Catalog.GetPaymentMethods;
using ControlDeFinanzasIA.Application.UseCases.Expenses.GetTransactions;
using ControlDeFinanzasIA.Application.UseCases.Expenses.RegisterExpense;
using ControlDeFinanzasIA.Application.UseCases.Expenses.UpdateExpense;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.ApplySavedView;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.DeleteSavedView;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.GetSavedViews;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.SaveCurrentFilter;
using ControlDeFinanzasIA.Application.UseCases.Transactions.DeleteTransaction;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Views;
using ControlDeFinanzasIA.UI.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// View-model for the expenses screen. Exposes the register/edit form, a paginated
/// list of expenses, filters, saved views, inline tag creation, field-level validation
/// errors and pagination commands.
/// </summary>
public sealed partial class ExpensesViewModel : ViewModelBase
{
    private readonly RegisterExpenseUseCase _registerUseCase;
    private readonly GetTransactionsQuery _getTransactionsQuery;
    private readonly UpdateExpenseUseCase _updateExpenseUseCase;
    private readonly DeleteTransactionUseCase _deleteTransactionUseCase;
    private readonly SaveCurrentFilterUseCase _saveCurrentFilterUseCase;
    private readonly ApplySavedViewUseCase _applySavedViewUseCase;
    private readonly GetSavedViewsUseCase _getSavedViewsUseCase;
    private readonly DeleteSavedViewUseCase _deleteSavedViewUseCase;
    private readonly CreateTagUseCase _createTagUseCase;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ITagRepository _tagRepository;
    private readonly GetPaymentMethodsQuery _getPaymentMethodsQuery;
    private readonly IClock _clock;
    private readonly ILogger<ExpensesViewModel> _logger;
    private readonly IStringLocalizer<Strings> _localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpensesViewModel"/> class.
    /// </summary>
    public ExpensesViewModel(
        RegisterExpenseUseCase registerUseCase,
        GetTransactionsQuery getTransactionsQuery,
        UpdateExpenseUseCase updateExpenseUseCase,
        DeleteTransactionUseCase deleteTransactionUseCase,
        SaveCurrentFilterUseCase saveCurrentFilterUseCase,
        ApplySavedViewUseCase applySavedViewUseCase,
        GetSavedViewsUseCase getSavedViewsUseCase,
        DeleteSavedViewUseCase deleteSavedViewUseCase,
        CreateTagUseCase createTagUseCase,
        ICategoryRepository categoryRepository,
        ITagRepository tagRepository,
        GetPaymentMethodsQuery getPaymentMethodsQuery,
        IClock clock,
        ILogger<ExpensesViewModel> logger,
        IStringLocalizer<Strings> localizer)
    {
        ArgumentNullException.ThrowIfNull(registerUseCase);
        ArgumentNullException.ThrowIfNull(getTransactionsQuery);
        ArgumentNullException.ThrowIfNull(updateExpenseUseCase);
        ArgumentNullException.ThrowIfNull(deleteTransactionUseCase);
        ArgumentNullException.ThrowIfNull(saveCurrentFilterUseCase);
        ArgumentNullException.ThrowIfNull(applySavedViewUseCase);
        ArgumentNullException.ThrowIfNull(getSavedViewsUseCase);
        ArgumentNullException.ThrowIfNull(deleteSavedViewUseCase);
        ArgumentNullException.ThrowIfNull(createTagUseCase);
        ArgumentNullException.ThrowIfNull(categoryRepository);
        ArgumentNullException.ThrowIfNull(tagRepository);
        ArgumentNullException.ThrowIfNull(getPaymentMethodsQuery);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(localizer);

        _registerUseCase = registerUseCase;
        _getTransactionsQuery = getTransactionsQuery;
        _updateExpenseUseCase = updateExpenseUseCase;
        _deleteTransactionUseCase = deleteTransactionUseCase;
        _saveCurrentFilterUseCase = saveCurrentFilterUseCase;
        _applySavedViewUseCase = applySavedViewUseCase;
        _getSavedViewsUseCase = getSavedViewsUseCase;
        _deleteSavedViewUseCase = deleteSavedViewUseCase;
        _createTagUseCase = createTagUseCase;
        _categoryRepository = categoryRepository;
        _tagRepository = tagRepository;
        _getPaymentMethodsQuery = getPaymentMethodsQuery;
        _clock = clock;
        _logger = logger;
        _localizer = localizer;

        _date = _clock.Today;
        _currentFilter = new ExpenseFilter { Page = 1, PageSize = 20 };

        _ = LoadCategoriesAsync();
        _ = LoadPaymentMethodsAsync();
        _ = LoadTagsAsync();
        _ = LoadSavedViewsAsync();
        _ = RefreshAsync();
    }

    /// <summary>
    /// Gets or sets the expense date.
    /// </summary>
    [ObservableProperty]
    private DateOnly? _date;

    /// <summary>
    /// Gets or sets the expense amount.
    /// </summary>
    [ObservableProperty]
    private decimal? _amount;

    /// <summary>
    /// Gets or sets the expense currency.
    /// </summary>
    [ObservableProperty]
    private Currency _currency = Currency.DOP;

    /// <summary>
    /// Gets or sets the selected category identifier.
    /// </summary>
    [ObservableProperty]
    private Guid? _selectedCategoryId;

    /// <summary>
    /// Gets or sets the expense description.
    /// </summary>
    [ObservableProperty]
    private string? _description;

    /// <summary>
    /// Gets or sets the expense note.
    /// </summary>
    [ObservableProperty]
    private string? _note;

    /// <summary>
    /// Gets or sets the expenses displayed on the current page.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ExpenseDto> _expenses = new();

    /// <summary>
    /// Gets or sets the selected expense in the ledger.
    /// </summary>
    [ObservableProperty]
    private ExpenseDto? _selectedExpense;

    /// <summary>
    /// Gets or sets the categories available for the category picker.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<CategoryDto> _categories = Array.Empty<CategoryDto>();

    /// <summary>
    /// Gets or sets the payment methods available for the payment-method picker.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<PaymentMethodDto> _paymentMethods = Array.Empty<PaymentMethodDto>();

    /// <summary>
    /// Gets or sets the selected payment method identifier. <c>null</c> when
    /// the user does not assign a payment method to the expense.
    /// </summary>
    [ObservableProperty]
    private Guid? _selectedPaymentMethodId;

    /// <summary>
    /// Gets or sets a value indicating whether the form is in edit mode.
    /// </summary>
    [ObservableProperty]
    private bool _isEditMode;

    /// <summary>
    /// Gets or sets the identifier of the expense being edited.
    /// </summary>
    [ObservableProperty]
    private Guid? _editableId;

    /// <summary>
    /// Gets or sets the identifier of the expense awaiting delete confirmation.
    /// </summary>
    [ObservableProperty]
    private Guid? _pendingDeleteExpenseId;

    /// <summary>
    /// Gets or sets the current page number (1-based).
    /// </summary>
    [ObservableProperty]
    private int _page = 1;

    /// <summary>
    /// Gets or sets the total number of pages available.
    /// </summary>
    [ObservableProperty]
    private int _totalPages;

    /// <summary>
    /// Gets or sets the validation errors keyed by property name.
    /// </summary>
    [ObservableProperty]
    private Dictionary<string, string> _validationErrors = new();

    /// <summary>
    /// Gets or sets the generic form-level error message.
    /// </summary>
    [ObservableProperty]
    private string? _formError;

    /// <summary>
    /// Gets or sets the transient status message shown after operations.
    /// </summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// Gets or sets the category identifier used by the filter.
    /// </summary>
    [ObservableProperty]
    private Guid? _filterCategoryId;

    /// <summary>
    /// Gets or sets the start date used by the filter.
    /// </summary>
    [ObservableProperty]
    private DateOnly? _filterDateFrom;

    /// <summary>
    /// Gets or sets the end date used by the filter.
    /// </summary>
    [ObservableProperty]
    private DateOnly? _filterDateTo;

    /// <summary>
    /// Gets or sets the filter applied to the expense query.
    /// </summary>
    [ObservableProperty]
    private ExpenseFilter _currentFilter;

    /// <summary>
    /// Gets or sets the tags available for the tag filter picker.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<SelectableTagItem> _availableTags = Array.Empty<SelectableTagItem>();

    /// <summary>
    /// Gets or sets the name of the new tag being created inline.
    /// </summary>
    [ObservableProperty]
    private string? _newTagName;

    /// <summary>
    /// Gets or sets the optional color of the new tag being created inline.
    /// </summary>
    [ObservableProperty]
    private string? _newTagColor;

    /// <summary>
    /// Gets or sets the error message shown when tag creation fails.
    /// </summary>
    [ObservableProperty]
    private string? _tagCreationError;

    /// <summary>
    /// Gets or sets the saved views available for the current entity type.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<SavedView> _savedViews = Array.Empty<SavedView>();

    /// <summary>
    /// Gets or sets the saved view selected in the filter rail.
    /// </summary>
    [ObservableProperty]
    private SavedView? _selectedSavedView;

    /// <summary>
    /// Gets or sets the name of the new saved view being created.
    /// </summary>
    [ObservableProperty]
    private string? _newViewName;

    /// <summary>
    /// Gets or sets the identifier of the saved view awaiting delete confirmation.
    /// </summary>
    [ObservableProperty]
    private Guid? _pendingDeleteViewId;

    /// <summary>
    /// Gets a value indicating whether the previous page command can execute.
    /// </summary>
    public bool CanGoToPreviousPage => Page > 1;

    /// <summary>
    /// Gets a value indicating whether the next page command can execute.
    /// </summary>
    public bool CanGoToNextPage => Page < TotalPages;

    /// <summary>
    /// Gets the localized page summary text.
    /// </summary>
    public string? PageSummary => string.Format(
        CultureInfo.CurrentCulture,
        _localizer["PageOfPagesFormat"],
        Page,
        TotalPages);

    /// <summary>
    /// Gets the identifiers of the currently selected tags.
    /// </summary>
    private IReadOnlyList<Guid>? SelectedTagIds
    {
        get
        {
            var selected = AvailableTags
                .Where(t => t.IsSelected)
                .Select(t => t.Id)
                .ToList();

            return selected.Count > 0 ? selected : null;
        }
    }

    /// <summary>
    /// Registers a new expense using the current form values.
    /// </summary>
    [RelayCommand]
    private async Task RegisterExpenseAsync()
    {
        ResetPendingStates();
        ValidationErrors.Clear();
        FormError = null;
        StatusMessage = null;

        RegisterExpenseCommand command = new(
            SelectedCategoryId ?? Guid.Empty,
            Date ?? _clock.Today,
            Amount ?? 0m,
            Currency,
            Description ?? string.Empty,
            Note,
            SelectedPaymentMethodId);

        Result result = await _registerUseCase.ExecuteAsync(command, default);

        if (result.IsSuccess)
        {
            Amount = null;
            Description = null;
            Note = null;
            Date = _clock.Today;
            ValidationErrors.Clear();
            FormError = null;
            StatusMessage = _localizer["RegistrationSuccessMessage"];
            await RefreshAsync();
        }
        else if (result.Error?.Code == "Validation")
        {
            ValidationErrors.Clear();
            if (result.Error.Details is not null)
            {
                foreach (KeyValuePair<string, string[]> detail in result.Error.Details)
                {
                    ValidationErrors[detail.Key] = string.Join("; ", detail.Value);
                }
            }
        }
        else
        {
            ValidationErrors.Clear();
            FormError = _localizer["FormErrorMessage"];
            _logger.LogError(
                "RegisterExpense failed with code {ErrorCode}: {ErrorMessage}",
                result.Error?.Code,
                result.Error?.Message);
        }
    }

    /// <summary>
    /// Loads the selected expense into the form for editing.
    /// </summary>
    [RelayCommand]
    private void BeginEdit(ExpenseDto dto)
    {
        ResetPendingStates();
        ValidationErrors.Clear();
        FormError = null;
        StatusMessage = null;

        Date = dto.Date;
        Amount = dto.Amount;
        Currency = dto.Currency;
        SelectedCategoryId = dto.CategoryId;
        SelectedPaymentMethodId = dto.PaymentMethodId;
        Description = dto.Description;
        Note = dto.Note;
        EditableId = dto.Id;
        IsEditMode = true;
    }

    /// <summary>
    /// Saves the changes made to the expense being edited.
    /// </summary>
    [RelayCommand]
    private async Task UpdateExpenseAsync()
    {
        if (!EditableId.HasValue)
        {
            return;
        }

        ResetPendingStates();
        ValidationErrors.Clear();
        FormError = null;
        StatusMessage = null;

        UpdateExpenseCommand command = new(
            EditableId.Value,
            SelectedCategoryId ?? Guid.Empty,
            Date ?? _clock.Today,
            Amount ?? 0m,
            Currency,
            Description ?? string.Empty,
            Note,
            SelectedPaymentMethodId);

        Result result = await _updateExpenseUseCase.ExecuteAsync(command, default);

        if (result.IsSuccess)
        {
            IsEditMode = false;
            EditableId = null;
            ClearForm();
            StatusMessage = _localizer["ExpenseUpdatedMessage"];
            await RefreshAsync();
        }
        else if (result.Error?.Code == "Validation")
        {
            ValidationErrors.Clear();
            if (result.Error.Details is not null)
            {
                foreach (KeyValuePair<string, string[]> detail in result.Error.Details)
                {
                    ValidationErrors[detail.Key] = string.Join("; ", detail.Value);
                }
            }
        }
        else
        {
            ValidationErrors.Clear();
            FormError = _localizer["FormErrorMessage"];
            _logger.LogError(
                "UpdateExpense failed with code {ErrorCode}: {ErrorMessage}",
                result.Error?.Code,
                result.Error?.Message);
        }
    }

    /// <summary>
    /// Aborts the current edit operation and clears the form.
    /// </summary>
    [RelayCommand]
    private void CancelEdit()
    {
        ResetPendingStates();
        IsEditMode = false;
        EditableId = null;
        ClearForm();
    }

    /// <summary>
    /// Deletes the selected expense after a two-step confirmation.
    /// </summary>
    [RelayCommand]
    private async Task DeleteExpenseAsync(ExpenseDto dto)
    {
        FormError = null;
        StatusMessage = null;

        if (PendingDeleteExpenseId != dto.Id)
        {
            ResetPendingStates();
            PendingDeleteExpenseId = dto.Id;
            return;
        }

        Result result = await _deleteTransactionUseCase.ExecuteAsync(
            new DeleteTransactionCommand(dto.Id, TransactionType.Expense),
            default);

        PendingDeleteExpenseId = null;

        if (result.IsSuccess)
        {
            StatusMessage = _localizer["TransactionDeletedMessage"];
            await RefreshAsync();
        }
        else
        {
            FormError = _localizer["FormErrorMessage"];
            _logger.LogError(
                "DeleteExpense failed with code {ErrorCode}: {ErrorMessage}",
                result.Error?.Code,
                result.Error?.Message);
        }
    }

    /// <summary>
    /// Two-step row delete entry-point exposed to the row delete button.
    /// First invocation arms the pending state (button label switches to
    /// "Confirmar"); second invocation on the same row executes the delete.
    /// </summary>
    [RelayCommand]
    private Task ConfirmDeleteAsync(ExpenseDto dto) => DeleteExpenseAsync(dto);

    /// <summary>
    /// Refreshes the paginated expense list for the current filter and page.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        Result result = await _getTransactionsQuery.ExecuteAsync(
            new GetTransactionsQueryRequest(
                Page,
                20,
                CurrentFilter.CategoryId,
                CurrentFilter.DateFrom,
                CurrentFilter.DateTo,
                CurrentFilter.TagIds),
            default);

        if (result.IsSuccess)
        {
            var paged = (PagedResult<ExpenseDto>)result.Value!;
            Expenses.Clear();
            foreach (ExpenseDto item in paged.Items)
            {
                Expenses.Add(item);
            }

            TotalPages = paged.TotalPages;
        }
        else
        {
            Expenses.Clear();
            TotalPages = 0;
            FormError = _localizer["FormErrorMessage"];
            _logger.LogError(
                "Refresh failed with code {ErrorCode}: {ErrorMessage}",
                result.Error?.Code,
                result.Error?.Message);
        }
    }

    /// <summary>
    /// Applies the filter defined by the filter rail controls and refreshes from page 1.
    /// </summary>
    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        ResetPendingStates();
        StatusMessage = null;
        Page = 1;
        CurrentFilter = new ExpenseFilter
        {
            Page = 1,
            PageSize = 20,
            CategoryId = FilterCategoryId,
            DateFrom = FilterDateFrom,
            DateTo = FilterDateTo,
            TagIds = SelectedTagIds,
        };

        await RefreshAsync();
    }

    /// <summary>
    /// Clears the active filter and refreshes from page 1.
    /// </summary>
    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        ResetPendingStates();
        StatusMessage = null;
        FilterCategoryId = null;
        FilterDateFrom = null;
        FilterDateTo = null;

        foreach (SelectableTagItem tag in AvailableTags)
        {
            tag.IsSelected = false;
        }

        Page = 1;
        CurrentFilter = new ExpenseFilter { Page = 1, PageSize = 20 };
        await RefreshAsync();
    }

    /// <summary>
    /// Saves the current filter as a new saved view.
    /// </summary>
    [RelayCommand]
    private async Task SaveViewAsync()
    {
        ResetPendingStates();
        FormError = null;

        if (string.IsNullOrWhiteSpace(NewViewName))
        {
            StatusMessage = _localizer["ViewNameRequiredMessage"];
            return;
        }

        Result result = await _saveCurrentFilterUseCase.ExecuteAsync(
            new SaveCurrentFilterCommand(NewViewName.Trim(), TransactionType.Expense, CurrentFilter),
            default);

        if (result.IsSuccess)
        {
            NewViewName = null;
            StatusMessage = _localizer["ViewSavedMessage"];
            _ = LoadSavedViewsAsync();
        }
        else
        {
            StatusMessage = _localizer["FormErrorMessage"];
            _logger.LogError(
                "SaveView failed with code {ErrorCode}: {ErrorMessage}",
                result.Error?.Code,
                result.Error?.Message);
        }
    }

    /// <summary>
    /// Applies the selected saved view to the filter rail and refreshes the list.
    /// </summary>
    [RelayCommand]
    private async Task ApplySavedViewAsync()
    {
        if (SelectedSavedView is null)
        {
            return;
        }

        ResetPendingStates();
        StatusMessage = null;

        Result result = await _applySavedViewUseCase.ExecuteAsync(
            new ApplySavedViewQuery(SelectedSavedView.Id),
            default);

        if (result.IsSuccess && result.Value is ApplySavedViewResult applyResult && applyResult.Filter is ExpenseFilter expenseFilter)
        {
            FilterCategoryId = expenseFilter.CategoryId;
            FilterDateFrom = expenseFilter.DateFrom;
            FilterDateTo = expenseFilter.DateTo;

            HashSet<Guid> selectedIds = expenseFilter.TagIds?.ToHashSet() ?? [];
            foreach (SelectableTagItem tag in AvailableTags)
            {
                tag.IsSelected = selectedIds.Contains(tag.Id);
            }

            Page = 1;
            CurrentFilter = expenseFilter with { Page = 1 };
            await RefreshAsync();
        }
        else
        {
            StatusMessage = _localizer["FormErrorMessage"];
            _logger.LogError(
                "ApplySavedView failed with code {ErrorCode}: {ErrorMessage}",
                result.Error?.Code,
                result.Error?.Message);
        }
    }

    /// <summary>
    /// Deletes the selected saved view after a two-step confirmation.
    /// </summary>
    [RelayCommand]
    private async Task DeleteSavedViewAsync()
    {
        if (SelectedSavedView is null)
        {
            return;
        }

        FormError = null;
        StatusMessage = null;

        if (PendingDeleteViewId != SelectedSavedView.Id)
        {
            ResetPendingStates();
            PendingDeleteViewId = SelectedSavedView.Id;
            return;
        }

        Result result = await _deleteSavedViewUseCase.ExecuteAsync(
            new DeleteSavedViewCommand(SelectedSavedView.Id),
            default);

        PendingDeleteViewId = null;

        if (result.IsSuccess)
        {
            SelectedSavedView = null;
            StatusMessage = _localizer["ViewDeletedMessage"];
            _ = LoadSavedViewsAsync();
        }
        else
        {
            StatusMessage = _localizer["FormErrorMessage"];
            _logger.LogError(
                "DeleteSavedView failed with code {ErrorCode}: {ErrorMessage}",
                result.Error?.Code,
                result.Error?.Message);
        }
    }

    /// <summary>
    /// Creates a new tag from the inline tag form and refreshes the tag picker.
    /// </summary>
    [RelayCommand]
    private async Task CreateTagAsync()
    {
        ResetPendingStates();
        FormError = null;
        StatusMessage = null;
        TagCreationError = null;

        if (string.IsNullOrWhiteSpace(NewTagName))
        {
            TagCreationError = _localizer["TagNameRequiredMessage"];
            return;
        }

        Result result = await _createTagUseCase.ExecuteAsync(
            new CreateTagCommand(NewTagName.Trim(), NewTagColor),
            default);

        if (result.IsSuccess)
        {
            NewTagName = null;
            NewTagColor = null;
            StatusMessage = _localizer["ViewSavedMessage"];
            _ = LoadTagsAsync();
        }
        else
        {
            TagCreationError = result.Error?.Code == "DuplicateName"
                ? _localizer["DuplicateTagMessage"]
                : _localizer["FormErrorMessage"];
            _logger.LogError(
                "CreateTag failed with code {ErrorCode}: {ErrorMessage}",
                result.Error?.Code,
                result.Error?.Message);
        }
    }

    /// <summary>
    /// Navigates to the previous page when possible.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private async Task PreviousPageAsync()
    {
        if (Page > 1)
        {
            Page--;
            await RefreshAsync();
        }
    }

    /// <summary>
    /// Navigates to the next page when possible.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private async Task NextPageAsync()
    {
        if (Page < TotalPages)
        {
            Page++;
            await RefreshAsync();
        }
    }

    /// <summary>
    /// Loads the category list from the repository.
    /// </summary>
    private async Task LoadCategoriesAsync()
    {
        IReadOnlyList<Category> items = await _categoryRepository.GetAllAsync(default);
        Categories = items.Select(c => c.ToDto()).ToList();
    }

    /// <summary>
    /// Loads the payment-method catalog via <see cref="GetPaymentMethodsQuery"/>.
    /// </summary>
    private async Task LoadPaymentMethodsAsync()
    {
        Result result = await _getPaymentMethodsQuery.ExecuteAsync(default);
        if (result.IsSuccess && result.Value is IReadOnlyList<PaymentMethodDto> methods)
        {
            PaymentMethods = methods;
        }
    }

    /// <summary>
    /// Loads the tag list from the repository while preserving the current selection.
    /// </summary>
    private async Task LoadTagsAsync()
    {
        IReadOnlyList<Tag> items = await _tagRepository.GetAllAsync(default);
        var selectedIds = AvailableTags
            .Where(t => t.IsSelected)
            .Select(t => t.Id)
            .ToHashSet();

        AvailableTags = items
            .Select(t => new SelectableTagItem(t.Id, t.Name, t.Color)
            {
                IsSelected = selectedIds.Contains(t.Id)
            })
            .ToList();
    }

    /// <summary>
    /// Loads the saved views of type Expense from the repository.
    /// </summary>
    private async Task LoadSavedViewsAsync()
    {
        Result result = await _getSavedViewsUseCase.ExecuteAsync(
            new GetSavedViewsQuery(TransactionType.Expense),
            default);

        if (result.IsSuccess && result.Value is IReadOnlyList<SavedView> views)
        {
            SavedViews = views;
        }
    }

    /// <summary>
    /// Clears the form fields and validation state.
    /// </summary>
    private void ClearForm()
    {
        Date = _clock.Today;
        Amount = null;
        Description = null;
        Note = null;
        SelectedCategoryId = null;
        SelectedPaymentMethodId = null;
        ValidationErrors.Clear();
        FormError = null;
    }

    /// <summary>
    /// Resets the pending delete states before starting a new action.
    /// </summary>
    private void ResetPendingStates()
    {
        PendingDeleteExpenseId = null;
        PendingDeleteViewId = null;
    }

    partial void OnPageChanged(int value)
    {
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnTotalPagesChanged(int value)
    {
        NextPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedSavedViewChanged(SavedView? value)
    {
        if (value is not null)
        {
            _ = ApplySavedViewAsync();
        }
    }
}
