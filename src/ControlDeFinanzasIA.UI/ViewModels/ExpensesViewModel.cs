using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Application.UseCases.Catalog.CreateTag;
using ControlDeFinanzasIA.Application.UseCases.Catalog.GetPaymentMethods;
using ControlDeFinanzasIA.Application.UseCases.Expenses.CategorizeExpense;
using ControlDeFinanzasIA.Application.UseCases.Expenses.GetTransactions;
using ControlDeFinanzasIA.Application.UseCases.Expenses.RegisterExpense;
using ControlDeFinanzasIA.Application.UseCases.Expenses.UpdateExpense;
using ControlDeFinanzasIA.Application.UseCases.Exports.Common;
using ControlDeFinanzasIA.Application.UseCases.Exports.ExportExpenseListReport;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.ApplySavedView;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.DeleteSavedView;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.GetSavedViews;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.SaveCurrentFilter;
using ControlDeFinanzasIA.Application.UseCases.Transactions.DeleteTransaction;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Domain.Views;
using ControlDeFinanzasIA.UI.Formatting;
using ControlDeFinanzasIA.UI.Input;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// View-model for the expenses screen. Exposes the register/edit form, a paginated
/// list of expenses, filters, saved views, inline tag creation, field-level validation
/// errors and pagination commands.
/// </summary>
public sealed partial class ExpensesViewModel : ViewModelBase, INavigationAware, IBusyAware
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
    private readonly ICurrencyConverter _currencyConverter;
    private readonly IClock _clock;
    private readonly ILogger<ExpensesViewModel> _logger;
    private readonly IStringLocalizer<Strings> _localizer;
    private readonly CategorizeExpenseUseCase? _categorizeExpenseUseCase;
    private readonly ExportExpenseListReportUseCase? _exportExpenseList;
    private readonly IFileSaveDialog? _fileSaveDialog;
    private bool _isNavigatedAway;

    /// <summary>
    /// Monotonically increasing token used by <see cref="RefreshAsync"/> to discard the result of a
    /// refresh that has been superseded by a newer one that started later but completed first (e.g.
    /// the constructor's initial unfiltered load racing with <see cref="ApplyDayFilter"/>'s filtered
    /// one during parameterized navigation drill-down, F-009). This is a pre-existing race — the same
    /// overlap already happens today between Apply and Clear in quick succession — the guard just
    /// makes it safe under any caller, not only calendar drill-down.
    /// </summary>
    private int _refreshGeneration;

    /// <summary>
    /// AI-suggested-category results already returned for a given normalized description in this form
    /// session (F-013, slice 6, D-12). Makes delete-and-retype free of extra calls.
    /// </summary>
    private readonly Dictionary<string, CategorySuggestionDto> _suggestionCache = new(StringComparer.Ordinal);

    /// <summary>
    /// Cancellation source owned by the currently in-flight <see cref="RequestSuggestionAsync"/>
    /// invocation, if any. Cancelled (never disposed) by a newer invocation or a manual supersede;
    /// each invocation disposes only its own instance in its own <c>finally</c> (D-12).
    /// </summary>
    private CancellationTokenSource? _suggestionCts;

    /// <summary>
    /// Monotonically increasing token that lets a superseded suggestion request detect it has been
    /// superseded and discard its result instead of rendering it (F-013, slice 6, D-12).
    /// </summary>
    private int _suggestionGeneration;

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
        ICurrencyConverter currencyConverter,
        IClock clock,
        ILogger<ExpensesViewModel> logger,
        IStringLocalizer<Strings> localizer,
        CategorizeExpenseUseCase? categorizeExpenseUseCase = null,
        ExportExpenseListReportUseCase? exportExpenseList = null,
        IFileSaveDialog? fileSaveDialog = null,
        ImportViewModel? import = null)
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
        ArgumentNullException.ThrowIfNull(currencyConverter);
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
        _currencyConverter = currencyConverter;
        _clock = clock;
        _logger = logger;
        _localizer = localizer;
        _categorizeExpenseUseCase = categorizeExpenseUseCase;
        _exportExpenseList = exportExpenseList;
        _fileSaveDialog = fileSaveDialog;
        Import = import;

        // Subscribed exactly once, here: the import view-model is owned by this instance (transient).
        if (import is not null)
        {
            import.CloseRequested += OnImportCloseRequested;
            import.Completed += OnImportCompleted;
            import.PropertyChanged += OnImportPropertyChanged;
        }

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
    /// Debounce interval before an AI category suggestion request fires (F-013, slice 6, D-12).
    /// Public and settable — not a constant — because there is no <c>InternalsVisibleTo</c> in this
    /// repo (so <c>internal</c> would be untestable) and a hardcoded 600 ms would make the suggestion
    /// test suite take seconds of pure sleeping. A test seam, documented as such.
    /// </summary>
    public TimeSpan SuggestionDebounceDelay { get; set; } = TimeSpan.FromMilliseconds(600);

    /// <summary>
    /// Gets or sets the category identifier suggested by the AI for the current <see cref="Description"/>.
    /// <c>null</c> when there is no suggestion to show. Only <see cref="AcceptSuggestionCommand"/> may
    /// copy this value into <see cref="SelectedCategoryId"/> (F-013, slice 6, R-5, D-12).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSuggestionVisible))]
    [NotifyPropertyChangedFor(nameof(SuggestedCategoryChipLabel))]
    private Guid? _suggestedCategoryId;

    /// <summary>
    /// Gets or sets the display name of the category suggested by the AI, matching <see cref="SuggestedCategoryId"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SuggestedCategoryChipLabel))]
    private string? _suggestedCategoryName;

    /// <summary>
    /// Gets or sets a value indicating whether a suggestion request is currently in flight.
    /// Dims the chip area — there is no loading spinner (D-12, D-13).
    /// </summary>
    [ObservableProperty]
    private bool _isSuggestionLoading;

    /// <summary>
    /// Gets or sets the non-blocking status label shown when a suggestion could not be produced
    /// (e.g. "IA no configurada" / "IA no disponible"). <c>null</c> when there is nothing to show.
    /// </summary>
    [ObservableProperty]
    private string? _aiStatusMessage;

    /// <summary>
    /// Gets a value indicating whether the suggestion chip should be rendered.
    /// </summary>
    public bool IsSuggestionVisible => SuggestedCategoryId is not null;

    /// <summary>
    /// Gets the localized chip label, e.g. "Suggested: Comida".
    /// </summary>
    public string SuggestedCategoryChipLabel =>
        $"{_localizer["ExpensesAiSuggestionPrefix"]} {SuggestedCategoryName}";

    /// <summary>
    /// Gets the localized accept-hint tooltip for the suggestion chip.
    /// </summary>
    public string SuggestedCategoryAcceptHint => _localizer["ExpensesAiSuggestionAcceptHint"];

    /// <summary>
    /// Gets the localized dismiss-hint tooltip for the suggestion chip's dismiss button.
    /// </summary>
    public string SuggestedCategoryDismissHint => _localizer["ExpensesAiSuggestionDismissHint"];

    /// <summary>
    /// Gets the localized "asking the AI…" label shown, in place of the chip, only while
    /// <see cref="IsSuggestionLoading"/> is <see langword="true"/> and no chip is displayed yet.
    /// There is no spinner (D-13) — this text is the only in-flight affordance.
    /// </summary>
    public string SuggestionLoadingLabel => _localizer["ExpensesAiSuggestionLoading"];

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
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    private int _page = 1;

    /// <summary>
    /// Gets or sets the total number of pages available.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmptyNoData))]
    [NotifyPropertyChangedFor(nameof(IsEmptyFiltered))]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    private int _totalPages;

    /// <summary>
    /// Gets or sets the number of records matching the applied filter across all pages.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecords))]
    private int _totalCount;

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
    /// Gets or sets the text searched inside the description by the filter.
    /// </summary>
    [ObservableProperty]
    private string? _filterText;

    /// <summary>
    /// Gets or sets the message shown when the filter inputs cannot be applied (e.g. an inverted date range).
    /// </summary>
    [ObservableProperty]
    private string? _filterError;

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
    [NotifyPropertyChangedFor(nameof(HasActiveFilter))]
    [NotifyPropertyChangedFor(nameof(IsFilterSummaryVisible))]
    [NotifyPropertyChangedFor(nameof(IsEmptyNoData))]
    [NotifyPropertyChangedFor(nameof(IsEmptyFiltered))]
    private ExpenseFilter _currentFilter;

    /// <summary>
    /// Gets or sets a value indicating whether the filter panel is expanded. The panel is collapsed by default behind
    /// a "Filters" toggle; <see cref="IsFilterSummaryVisible"/> keeps an applied filter visible while it is collapsed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilterSummaryVisible))]
    private bool _isFilterPanelOpen;

    /// <summary>
    /// Gets a value indicating whether the collapsed-panel summary ("filters applied" plus a clear action) is shown:
    /// a filter narrows the list but the panel that edits it is closed.
    /// </summary>
    public bool IsFilterSummaryVisible => HasActiveFilter && !IsFilterPanelOpen;

    /// <summary>Expands or collapses the filter panel.</summary>
    [RelayCommand]
    private void ToggleFilterPanel() => IsFilterPanelOpen = !IsFilterPanelOpen;


    /// <summary>
    /// Gets or sets the tags available for the tag filter picker.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<SelectableTagItem> _availableTags = Array.Empty<SelectableTagItem>();

    /// <summary>
    /// Gets or sets the payment methods available for the filter picker.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<SelectablePaymentMethodItem> _availablePaymentMethods = Array.Empty<SelectablePaymentMethodItem>();

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
    [NotifyPropertyChangedFor(nameof(IsSavedViewDeletePending))]
    [NotifyPropertyChangedFor(nameof(SavedViewDeleteLabel))]
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
    [NotifyPropertyChangedFor(nameof(IsSavedViewDeletePending))]
    [NotifyPropertyChangedFor(nameof(SavedViewDeleteLabel))]
    private Guid? _pendingDeleteViewId;

    /// <summary>
    /// Gets a value indicating whether the selected saved view is awaiting delete confirmation.
    /// </summary>
    public bool IsSavedViewDeletePending => SelectedSavedView is { } view && PendingDeleteViewId == view.Id;

    /// <summary>
    /// Gets the text of the saved-view delete button: "Delete view", or "Confirm" once the delete is armed.
    /// </summary>
    public string SavedViewDeleteLabel => _localizer[IsSavedViewDeletePending ? "ConfirmDeleteButton" : "DeleteViewButton"];

    /// <summary>
    /// Gets a value indicating whether at least one record matches the applied filter (drives the pager).
    /// </summary>
    public bool HasRecords => TotalCount > 0;

    /// <summary>
    /// Disarms the saved-view delete confirmation without deleting anything.
    /// </summary>
    [RelayCommand]
    private void CancelSavedViewDelete() => PendingDeleteViewId = null;

    /// <summary>
    /// Gets a value indicating whether the list has been loaded successfully at least once. Until then no empty
    /// state is shown, so a slow or failed first load never flashes "no data yet" (F-021, DD-9).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmptyNoData))]
    [NotifyPropertyChangedFor(nameof(IsEmptyFiltered))]
    private bool _hasLoaded;

    /// <summary>
    /// Gets a value indicating whether the APPLIED filter narrows the list, i.e. it differs from the default
    /// filter on the dimensions the list query honors (category, dates, tags). Edits in the filter rail that were
    /// not applied do not count, and neither does the payment-method selection, which the list ignores.
    /// </summary>
    public bool HasActiveFilter =>
        CurrentFilter.CategoryId is not null
        || CurrentFilter.DateFrom is not null
        || CurrentFilter.DateTo is not null
        || CurrentFilter.TagIds is { Count: > 0 }
        || !string.IsNullOrWhiteSpace(CurrentFilter.Text);

    /// <summary>
    /// Gets a value indicating whether the list is loaded, empty, and no filter is narrowing it: there is no data yet.
    /// </summary>
    public bool IsEmptyNoData => IsListEmpty && !HasActiveFilter;

    /// <summary>
    /// Gets a value indicating whether the list is loaded and empty only because the applied filter matches nothing.
    /// </summary>
    public bool IsEmptyFiltered => IsListEmpty && HasActiveFilter;

    private bool IsListEmpty => HasLoaded && Expenses.Count == 0 && TotalPages == 0;

    /// <summary>
    /// Gets the focus request that moves keyboard focus to the description box. A NEW token is assigned on every
    /// <see cref="BeginNewEntry"/> so the change notification is always raised (F-021, DD-5).
    /// </summary>
    [ObservableProperty]
    private FocusRequestToken? _descriptionFocusRequest;

    /// <summary>
    /// Starts a new entry: cancels an edit in progress (an unsaved new entry is left untouched) and asks the view
    /// to focus the description box. Bound to the empty-state create action and, later, to <c>Ctrl+N</c>.
    /// </summary>
    [RelayCommand]
    public void BeginNewEntry()
    {
        if (IsEditMode)
        {
            CancelEdit();
        }

        DescriptionFocusRequest = new FocusRequestToken();
    }

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
    /// Gets the identifiers of the currently selected payment methods.
    /// </summary>
    private IReadOnlyList<Guid>? SelectedPaymentMethodIds
    {
        get
        {
            var selected = AvailablePaymentMethods
                .Where(m => m.IsSelected)
                .Select(m => m.Id)
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

            // F-021 (R-14): keyboard users keep typing the next entry without reaching for the mouse.
            DescriptionFocusRequest = new FocusRequestToken();
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

        // Fase 13 (F-013, slice 6, D-12): stop any pending/in-flight suggestion before touching the
        // form. SelectedCategoryId is assigned BEFORE Description below — load-bearing ordering: it
        // makes OnSelectedCategoryIdChanged's guard true before OnDescriptionChanged fires, so opening
        // an already-categorized expense for editing never triggers an AI call (S-6, R-e).
        CancelSuggestion();
        ClearSuggestion();

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
    /// Accepts the currently displayed AI suggestion, writing it into <see cref="SelectedCategoryId"/>.
    /// This is the ONLY code path in the feature that writes <see cref="SelectedCategoryId"/> from AI
    /// output (F-013, slice 6, R-5, S-5, D-12).
    /// </summary>
    [RelayCommand]
    private void AcceptSuggestion()
    {
        if (SuggestedCategoryId is not { } id)
        {
            return;
        }

        SelectedCategoryId = id;
        ClearSuggestion();
    }

    /// <summary>
    /// Dismisses the currently displayed or in-flight AI suggestion without touching
    /// <see cref="SelectedCategoryId"/> (F-013, slice 6, R-5, S-7, D-12).
    /// </summary>
    [RelayCommand]
    private void DismissSuggestion()
    {
        CancelSuggestion();
        ClearSuggestion();
    }

    /// <summary>
    /// Two-step row delete entry-point exposed to the row delete button.
    /// First invocation arms the pending state (button label switches to
    /// "Confirmar"); second invocation on the same row executes the delete.
    /// </summary>
    [RelayCommand]
    private Task ConfirmDeleteAsync(ExpenseDto dto) => DeleteExpenseAsync(dto);

    /// <summary>
    /// Disarms the row awaiting delete confirmation without deleting anything.
    /// </summary>
    [RelayCommand]
    private void CancelDelete() => PendingDeleteExpenseId = null;

    /// <summary>
    /// Gets or sets a value indicating whether a report export is in flight. Shared by both export
    /// commands: it is the single source of truth for the double-invocation guard (F-018, D-11).
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportPdfCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportExcelCommand))]
    private bool _isExporting;

    /// <summary>Exports the current filter (all pages) as a PDF report.</summary>
    [RelayCommand(CanExecute = nameof(CanExport), AllowConcurrentExecutions = true)]
    private Task ExportPdfAsync() => ExportAsync(ReportFormat.Pdf);

    /// <summary>Exports the current filter (all pages) as an Excel workbook.</summary>
    [RelayCommand(CanExecute = nameof(CanExport), AllowConcurrentExecutions = true)]
    private Task ExportExcelAsync() => ExportAsync(ReportFormat.Xlsx);

    private bool CanExport() => !IsExporting && _exportExpenseList is not null && _fileSaveDialog is not null;

    /// <summary>Gets the hosted import panel view-model; <c>null</c> when the screen was composed without it (F-020).</summary>
    public ImportViewModel? Import { get; }

    /// <summary>
    /// Gets a value indicating whether an import is loading or committing. Reads the hosted panel's existing flag
    /// (no state of its own) so window shortcuts never interrupt a commit (F-021, R-13).
    /// </summary>
    public bool IsBusy => Import?.IsBusy == true;

    /// <summary>Gets a value indicating whether the import feature is available on this screen.</summary>
    public bool HasImport => Import is not null;

    /// <summary>Gets or sets a value indicating whether the import panel replaces the movement list (F-020, R-22).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseImportCommand))]
    private bool _isImportOpen;

    partial void OnIsExportingChanged(bool value) => OpenImportCommand.NotifyCanExecuteChanged();

    /// <summary>Opens the import panel and starts the file picker. Does nothing while the panel is open or busy.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenImport))]
    private async Task OpenImportAsync()
    {
        // The real guard: ExecuteAsync does not consult CanExecute.
        if (!CanOpenImport() || Import is null || Import.State != ImportPanelState.Idle)
        {
            return;
        }

        IsImportOpen = true;
        await Import.ChooseFileCommand.ExecuteAsync(null);
    }

    private bool CanOpenImport() => Import is not null && !IsImportOpen && !IsExporting;

    /// <summary>
    /// Dismisses the import panel from the host: cancels a loaded file, closes a finished result and
    /// closes an idle panel left open by a failed load. Does nothing while the import is being written.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCloseImport))]
    private void CloseImport()
    {
        if (Import is null || !IsImportOpen)
        {
            return;
        }

        switch (Import.State)
        {
            case ImportPanelState.Importing:
                return;
            case ImportPanelState.Done:
                Import.CloseCommand.Execute(null);
                break;
            case ImportPanelState.Idle:
                break;
            default:
                Import.CancelCommand.Execute(null);
                break;
        }

        IsImportOpen = false;
    }

    private bool CanCloseImport() => Import is not null && IsImportOpen && Import.State != ImportPanelState.Importing;

    /// <inheritdoc />
    public void OnNavigatedTo() => _isNavigatedAway = false;

    /// <summary>
    /// Drops the picked file when the user leaves the screen: the navigation history keeps this instance
    /// alive, so nothing may stay retained in an off-screen panel (F-020).
    /// </summary>
    public void OnNavigatedFrom()
    {
        _isNavigatedAway = true;
        CloseImport();
    }

    private void OnImportCloseRequested(object? sender, EventArgs e) => IsImportOpen = false;

    private void OnImportCompleted(object? sender, ImportCompletedEventArgs e) => _ = ReloadAfterImportAsync();

    private void OnImportPropertyChanged(object? sender, global::System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ImportViewModel.State))
        {
            return;
        }

        CloseImportCommand.NotifyCanExecuteChanged();

        // A commit that was running when the user left may end in any state: never keep bytes off-screen.
        if (_isNavigatedAway)
        {
            CloseImport();
        }
    }

    private async Task ReloadAfterImportAsync()
    {
        try
        {
            await LoadCategoriesAsync();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to reload the expenses after an import.");
        }
    }

    /// <summary>Maps a use-case failure code to the resx key of its localized message.</summary>
    internal static string ExportErrorKey(string? code) => code switch
    {
        ReportErrorCodes.RenderFailed => "ExportRenderFailedMessage",
        ReportErrorCodes.SaveFailed => "ExportSaveFailedMessage",
        ReportErrorCodes.DataFailed => "ExportDataFailedMessage",
        ReportErrorCodes.WriterUnavailable => "ExportWriterUnavailableMessage",
        _ => "ExportFailedMessage",
    };

    private async Task ExportAsync(ReportFormat format)
    {
        // The real guard: ExecuteAsync does not consult CanExecute, and the two commands have
        // independent single-flight state, so the shared flag is checked before the first await.
        if (IsExporting || _exportExpenseList is null || _fileSaveDialog is null)
        {
            return;
        }

        IsExporting = true;
        FormError = null;
        StatusMessage = null;

        try
        {
            // Captured up front: editing the filter while the dialog is open must not retarget the
            // export. PaymentMethodIds is dropped because the visible list ignores it (Reconciliation #4);
            // Page/PageSize are overwritten by the use case.
            ExpenseFilter filter = CurrentFilter with { PaymentMethodIds = null };

            using SaveTarget? target = await _fileSaveDialog.PickAsync(SuggestedExportFileName(format), format);
            if (target is null)
            {
                // Cancelled: nothing is written and no message is shown.
                return;
            }

            Result result = await _exportExpenseList.ExecuteAsync(
                new ExportExpenseListReportCommand(filter, format, ReportLabelsFactory.Create()),
                target);

            if (result.IsSuccess)
            {
                StatusMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    _localizer["ExportSucceededMessage"].Value,
                    target.DisplayName);
            }
            else
            {
                FormError = _localizer[ExportErrorKey(result.Error?.Code)];
                _logger.LogWarning(
                    "Expense list export failed: {ErrorCode} {ErrorMessage}",
                    result.Error?.Code,
                    result.Error?.Message);
            }
        }
        catch (OperationCanceledException)
        {
            // Not a failure: no message.
        }
        catch (Exception ex)
        {
            FormError = _localizer["ExportFailedMessage"];
            _logger.LogError(ex, "Unable to export the expense list.");
        }
        finally
        {
            IsExporting = false;
        }
    }

    private string SuggestedExportFileName(ReportFormat format)
    {
        string extension = format == ReportFormat.Pdf ? ".pdf" : ".xlsx";
        string stamp = _clock.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return string.Format(CultureInfo.CurrentCulture, _localizer["ExportExpenseListFileName"].Value, stamp) + extension;
    }

    /// <summary>
    /// Refreshes the paginated expense list for the current filter and page.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        int generation = ++_refreshGeneration;

        Result result = await _getTransactionsQuery.ExecuteAsync(
            new GetTransactionsQueryRequest(
                Page,
                20,
                CurrentFilter.CategoryId,
                CurrentFilter.DateFrom,
                CurrentFilter.DateTo,
                CurrentFilter.TagIds,
                CurrentFilter.Text),
            default);

        PagedResult<ExpenseDto>? paged = null;
        if (result.IsSuccess)
        {
            paged = (PagedResult<ExpenseDto>)result.Value!;
            await EnrichWithConversionAsync(paged.Items, default);
        }

        // A newer refresh started and possibly completed while this one was awaiting; its result is
        // stale and must not clobber whatever the newer generation already wrote.
        if (generation != _refreshGeneration)
        {
            return;
        }

        // The requested page no longer exists (the last row of the last page was deleted, or a filter narrowed the
        // list): step back to the new last page instead of showing an empty page "3 of 2".
        if (result.IsSuccess && paged!.Items.Count == 0 && Page > 1)
        {
            Page = Math.Max(1, paged.TotalPages);
            await RefreshAsync();
            return;
        }

        if (result.IsSuccess)
        {
            Expenses.Clear();
            foreach (ExpenseDto item in paged!.Items)
            {
                Expenses.Add(item);
            }

            TotalCount = paged.Total;
            TotalPages = paged.TotalPages;
            HasLoaded = true;
        }
        else
        {
            Expenses.Clear();
            TotalCount = 0;
            TotalPages = 0;
            FormError = _localizer["FormErrorMessage"];
            _logger.LogError(
                "Refresh failed with code {ErrorCode}: {ErrorMessage}",
                result.Error?.Code,
                result.Error?.Message);
        }

        // The empty flags depend on the collection count, which raises no property notification of its own.
        OnPropertyChanged(nameof(IsEmptyNoData));
        OnPropertyChanged(nameof(IsEmptyFiltered));
    }

    /// <summary>
    /// Sets the expense filter to a single exact day and applies it, refreshing the list from page 1.
    /// </summary>
    /// <remarks>
    /// Intended as the <c>configure</c> delegate target for parameterized navigation drill-down from
    /// the calendar view (F-009): <c>INavigationService.NavigateTo&lt;ExpensesViewModel&gt;</c> calls
    /// this after the view-model is resolved but before it becomes the active view-model. Setting
    /// <see cref="FilterDateFrom"/>/<see cref="FilterDateTo"/> alone does not reload the list — there
    /// are no property-changed partials for those properties — so <see cref="ApplyFilterCommand"/>
    /// must be invoked explicitly.
    /// </remarks>
    /// <param name="day">The exact day to filter by; used for both <c>DateFrom</c> and <c>DateTo</c>.</param>
    public void ApplyDayFilter(DateOnly day) => ApplyDateRangeFilter(day, day);

    /// <summary>
    /// Sets the expense filter to an inclusive date range and applies it, refreshing the list from page 1.
    /// </summary>
    /// <remarks>
    /// Same drill-down contract as <see cref="ApplyDayFilter"/>; used by the Dashboard's "view all"
    /// action to open this list on the displayed month.
    /// </remarks>
    /// <param name="from">The first day of the range.</param>
    /// <param name="to">The last day of the range.</param>
    public void ApplyDateRangeFilter(DateOnly from, DateOnly to)
    {
        FilterDateFrom = from;
        FilterDateTo = to;
        _ = ApplyFilterCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Applies the filter defined by the filter rail controls and refreshes from page 1.
    /// </summary>
    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        ResetPendingStates();
        StatusMessage = null;

        if (FilterDateFrom is { } from && FilterDateTo is { } to && from > to)
        {
            FilterError = _localizer["FilterDateRangeInvalidMessage"];
            return;
        }

        FilterError = null;
        Page = 1;
        CurrentFilter = new ExpenseFilter
        {
            Page = 1,
            PageSize = 20,
            CategoryId = FilterCategoryId,
            DateFrom = FilterDateFrom,
            DateTo = FilterDateTo,
            Text = string.IsNullOrWhiteSpace(FilterText) ? null : FilterText.Trim(),
            TagIds = SelectedTagIds,
            PaymentMethodIds = SelectedPaymentMethodIds,
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
        FilterError = null;
        FilterText = null;
        FilterCategoryId = null;
        FilterDateFrom = null;
        FilterDateTo = null;

        foreach (SelectableTagItem tag in AvailableTags)
        {
            tag.IsSelected = false;
        }

        foreach (SelectablePaymentMethodItem method in AvailablePaymentMethods)
        {
            method.IsSelected = false;
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
            FilterError = null;
            FilterText = expenseFilter.Text;
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
    /// <summary>
    /// Loads the payment-method catalog for both the form picker and the
    /// filter rail. Preserves the current selection across refreshes.
    /// </summary>
    private async Task LoadPaymentMethodsAsync()
    {
        Result result = await _getPaymentMethodsQuery.ExecuteAsync(default);
        if (result.IsSuccess && result.Value is IReadOnlyList<PaymentMethodDto> methods)
        {
            var selectedIds = AvailablePaymentMethods
                .Where(m => m.IsSelected)
                .Select(m => m.Id)
                .ToHashSet();

            AvailablePaymentMethods = methods
                .Select(m => new SelectablePaymentMethodItem(m.Id, m.Name, m.IsSystem)
                {
                    IsSelected = selectedIds.Contains(m.Id),
                })
                .ToList();

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
    /// Populates <see cref="ExpenseDto.ConvertedAmountDisplay"/> for each
    /// item by consulting <see cref="ICurrencyConverter"/>. Failures
    /// are swallowed (Display = null) so a single bad item doesn't
    /// break the whole page render.
    /// </summary>
    /// <param name="items">DTOs to enrich in-place.</param>
    /// <param name="ct">Token de cancelacion.</param>
    private async Task EnrichWithConversionAsync(IReadOnlyList<ExpenseDto> items, CancellationToken ct)
    {
        foreach (ExpenseDto item in items)
        {
            string? text;
            try
            {
                MoneyDisplay display = await _currencyConverter.DisplayAsync(
                    Money.Create(item.Amount, item.Currency), ct);
                text = display.Converted is null
                    ? null
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.ApproximateFormat,
                        $"{display.Converted.Amount:N2} {display.Converted.Currency}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Conversion skipped for {ExpenseId}.", item.Id);
                text = null;
            }

            typeof(ExpenseDto)
                .GetProperty(nameof(ExpenseDto.ConvertedAmountDisplay))?
                .SetValue(item, text);
        }
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
        CancelSuggestion();
        ClearSuggestion();

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
    /// Discards the currently displayed/pending suggestion (chip and status message untouched by
    /// design — callers that need to clear <see cref="AiStatusMessage"/> too do so explicitly).
    /// Does NOT cancel any in-flight request — see <see cref="CancelSuggestion"/> for that (F-013,
    /// slice 6, D-12).
    /// </summary>
    private void ClearSuggestion()
    {
        SuggestedCategoryId = null;
        SuggestedCategoryName = null;
    }

    /// <summary>
    /// Invalidates the currently in-flight suggestion request, if any: bumps the generation token so
    /// its eventual completion is recognized as superseded and discarded even if cancellation itself
    /// races past a completed response, and cancels its <see cref="CancellationTokenSource"/> (F-013,
    /// slice 6, D-12).
    /// </summary>
    private void CancelSuggestion()
    {
        // Bumping the generation here (rather than only inside RequestSuggestionAsync) means a
        // request that is invalidated by a manual pick/dismiss/form reset will never pass its own
        // "generation still current" check again — so it must not be the one responsible for
        // resetting IsSuggestionLoading either; that would leave it stuck true forever whenever
        // CancelSuggestion() runs without a fresh RequestSuggestionAsync starting right behind it
        // (e.g. a manual category pick while a request is in flight, R-c).
        Interlocked.Increment(ref _suggestionGeneration);
        IsSuggestionLoading = false;

        try
        {
            _suggestionCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The owning invocation already completed and disposed its own CTS (see the
            // CompareExchange in RequestSuggestionAsync's finally) between the null-check above and
            // this call — nothing left to cancel.
        }
    }

    /// <summary>
    /// Applies the outcome of a <see cref="CategorizeExpenseUseCase"/> round trip to the observable
    /// suggestion state (F-013, slice 6, R-5, R-6, D-12).
    /// </summary>
    private void ApplySuggestionResult(Result result)
    {
        if (result.IsSuccess && result.Value is CategorySuggestionDto dto)
        {
            if (dto.Matched)
            {
                SuggestedCategoryId = dto.CategoryId;
                SuggestedCategoryName = dto.CategoryName;
            }
            else
            {
                ClearSuggestion();
            }

            AiStatusMessage = null;
            return;
        }

        ClearSuggestion();
        AiStatusMessage = result.Error?.Code == "AiNotConfigured"
            ? _localizer["ExpensesAiNotConfiguredMessage"]
            : _localizer["ExpensesAiUnavailableMessage"];
    }

    /// <summary>
    /// Debounces, guards, and executes an AI category suggestion request for <paramref name="text"/>
    /// (F-013, slice 6, R-5, R-6, D-12). Fire-and-forget from <see cref="OnDescriptionChanged"/>; the
    /// entire body is wrapped so no exception — including a superseding cancellation — ever escapes
    /// as an unobserved task exception or reaches the UI as a crash.
    /// </summary>
    private async Task RequestSuggestionAsync(string? text)
    {
        int generation = Interlocked.Increment(ref _suggestionGeneration);

        var fresh = new CancellationTokenSource();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _suggestionCts, fresh);
        previous?.Cancel();
        CancellationToken token = fresh.Token;

        try
        {
            if (_categorizeExpenseUseCase is null || SelectedCategoryId is not null)
            {
                return;
            }

            if ((text ?? string.Empty).Trim().Length < CategorizeExpenseUseCase.MinimumDescriptionLength)
            {
                ClearSuggestion();
                return;
            }

            await Task.Delay(SuggestionDebounceDelay, token);

            // State can and does change during the debounce window; a guard checked only before the
            // delay is a guard that is not checked (D-12).
            if (generation != Volatile.Read(ref _suggestionGeneration)
                || _categorizeExpenseUseCase is null
                || SelectedCategoryId is not null)
            {
                return;
            }

            string normalized = (Description ?? string.Empty).Trim();
            if (normalized.Length < CategorizeExpenseUseCase.MinimumDescriptionLength)
            {
                ClearSuggestion();
                return;
            }

            if (_suggestionCache.TryGetValue(normalized, out CategorySuggestionDto? cached))
            {
                ApplySuggestionResult(Result.Success(cached));
                return;
            }

            IsSuggestionLoading = true;
            AiStatusMessage = null;

            Result result = await _categorizeExpenseUseCase.ExecuteAsync(
                new CategorizeExpenseCommand(normalized), token);

            // A superseded response must never render.
            if (generation != Volatile.Read(ref _suggestionGeneration))
            {
                return;
            }

            if (result.IsSuccess && result.Value is CategorySuggestionDto dto)
            {
                _suggestionCache[normalized] = dto;
            }

            ApplySuggestionResult(result);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer keystroke or a manual pick — swallowed, nothing renders (D-12).
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AI suggestion request failed unexpectedly.");
            if (generation == Volatile.Read(ref _suggestionGeneration))
            {
                AiStatusMessage = _localizer["ExpensesAiUnavailableMessage"];
            }
        }
        finally
        {
            if (generation == Volatile.Read(ref _suggestionGeneration))
            {
                IsSuggestionLoading = false;
            }

            // Only clear the shared field if a newer invocation hasn't already replaced it with its
            // own CTS — otherwise a later CancelSuggestion() call (e.g. Accept/Dismiss/manual pick,
            // with nothing new typed afterwards) would try to Cancel() this now-disposed instance.
            Interlocked.CompareExchange(ref _suggestionCts, null, fresh);
            fresh.Dispose();
        }
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

    /// <summary>
    /// Debounced AI category suggestion entry point (F-013, slice 6, D-12). Fire-and-forget:
    /// <see cref="RequestSuggestionAsync"/> owns its own generation/cancellation bookkeeping and
    /// never lets an exception escape.
    /// </summary>
    partial void OnDescriptionChanged(string? value) => _ = RequestSuggestionAsync(value);

    /// <summary>
    /// Manually picking a category supersedes any pending/in-flight suggestion (D1: manual pick wins).
    /// </summary>
    partial void OnSelectedCategoryIdChanged(Guid? value)
    {
        if (value is not null)
        {
            CancelSuggestion();
            ClearSuggestion();
        }
    }
}
