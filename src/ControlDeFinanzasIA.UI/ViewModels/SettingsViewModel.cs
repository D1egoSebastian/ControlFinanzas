using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Settings.GetAppSettings;
using ControlDeFinanzasIA.Application.UseCases.Settings.TestAiConnection;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateAiSettings;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateLocale;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateTheme;
using ControlDeFinanzasIA.UI.Resources;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// View-model for the Settings screen (F-012, D-10): a sectioned
/// preferences form, not a list-CRUD screen. Each section (Apariencia,
/// Idioma, IA, Acerca de) has its own save affordance mapping 1:1 to its
/// own update use case — there is no single global "Save all" command.
/// Acerca de has no save command; it only displays <see cref="AppVersion"/>.
/// </summary>
/// <remarks>
/// Deliberately free of any Avalonia-specific type (D-8): the theme
/// picker only persists a preference via <see cref="UpdateThemeUseCase"/>
/// and never touches <c>Avalonia.Application.Current.RequestedThemeVariant</c>.
/// This keeps the view-model constructible and testable outside an
/// Avalonia headless harness.
/// </remarks>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly GetAppSettingsQuery _getAppSettingsQuery;
    private readonly UpdateThemeUseCase _updateThemeUseCase;
    private readonly UpdateLocaleUseCase _updateLocaleUseCase;
    private readonly UpdateAiSettingsUseCase _updateAiSettingsUseCase;
    private readonly TestAiConnectionUseCase _testAiConnectionUseCase;
    private readonly ILogger<SettingsViewModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/>
    /// class and kicks off the initial load, following the same
    /// fire-and-forget constructor-load pattern as
    /// <see cref="BudgetsViewModel"/>.
    /// </summary>
    public SettingsViewModel(
        GetAppSettingsQuery getAppSettingsQuery,
        UpdateThemeUseCase updateThemeUseCase,
        UpdateLocaleUseCase updateLocaleUseCase,
        UpdateAiSettingsUseCase updateAiSettingsUseCase,
        TestAiConnectionUseCase testAiConnectionUseCase,
        ILogger<SettingsViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(getAppSettingsQuery);
        ArgumentNullException.ThrowIfNull(updateThemeUseCase);
        ArgumentNullException.ThrowIfNull(updateLocaleUseCase);
        ArgumentNullException.ThrowIfNull(updateAiSettingsUseCase);
        ArgumentNullException.ThrowIfNull(testAiConnectionUseCase);
        ArgumentNullException.ThrowIfNull(logger);

        _getAppSettingsQuery = getAppSettingsQuery;
        _updateThemeUseCase = updateThemeUseCase;
        _updateLocaleUseCase = updateLocaleUseCase;
        _updateAiSettingsUseCase = updateAiSettingsUseCase;
        _testAiConnectionUseCase = testAiConnectionUseCase;
        _logger = logger;

        Themes = Enum.GetValues<AppTheme>();
        AiProviders = Enum.GetValues<AiProvider>();
        Locales = SupportedLocales.All;
        AppVersion = ReadInformationalVersion();

        // Display-projection records for the pickers (D-12): the raw
        // Themes/Locales collections stay enum/string-typed for callers
        // and tests, while these carry the localized label the ComboBox
        // actually renders. No IValueConverter, per the design's note.
        ThemeOptions = Themes
            .Select(theme => new ThemeOption(theme, DisplayNameForTheme(theme)))
            .ToList();
        LocaleOptions = Locales
            .Select(locale => new LocaleOption(locale, DisplayNameForLocale(locale)))
            .ToList();

        _ = LoadAsync();
    }

    // --- Apariencia ---

    /// <summary>Gets or sets the theme preference selected in the picker.</summary>
    [ObservableProperty]
    private AppTheme _theme = AppSettings.DefaultTheme;

    // --- Idioma ---

    /// <summary>Gets or sets the language preference selected in the picker.</summary>
    [ObservableProperty]
    private string _locale = SupportedLocales.Default;

    /// <summary>
    /// Gets or sets a value indicating whether a restart-required notice
    /// should be shown. Set to <see langword="true"/> only after
    /// <see cref="SaveLocaleAsync"/> succeeds (Q2's hard requirement that
    /// the notice appear after a successful save, not as ambient helper
    /// text). Never touched by <see cref="SaveThemeAsync"/> or the IA
    /// save command.
    /// </summary>
    [ObservableProperty]
    private bool _isRestartRequired;

    // --- IA (persistence-only fields, hydrated now; SaveAiCommand lands in slice 4b) ---

    /// <summary>Gets or sets the configured AI provider.</summary>
    [ObservableProperty]
    private AiProvider _aiProvider = AppSettings.DefaultAiProvider;

    /// <summary>Gets or sets the configured AI API key.</summary>
    [ObservableProperty]
    private string _aiApiKey = string.Empty;

    /// <summary>Gets or sets the configured AI model name.</summary>
    [ObservableProperty]
    private string _aiModel = string.Empty;

    /// <summary>Gets or sets the configured AI base URL, or <see langword="null"/> for the provider default.</summary>
    [ObservableProperty]
    private string? _aiBaseUrl;

    /// <summary>Gets or sets the configured AI sampling temperature.</summary>
    [ObservableProperty]
    private double _aiTemperature = AppSettings.DefaultAiTemperature;

    /// <summary>Gets or sets the configured maximum tokens per AI request.</summary>
    [ObservableProperty]
    private int _aiMaxTokensPerRequest = AppSettings.DefaultAiMaxTokensPerRequest;

    /// <summary>
    /// Gets or sets a value indicating whether "Probar conexión" is
    /// currently in flight (F-013, slice 7, R-8). Drives
    /// <see cref="CanTestConnection"/> and the button's busy label.
    /// </summary>
    [ObservableProperty]
    private bool _isTestingConnection;

    /// <summary>
    /// Gets or sets the localized result message of the last "Probar
    /// conexión" run (F-013, slice 7, R-8). Empty before the button has
    /// ever been clicked.
    /// </summary>
    [ObservableProperty]
    private string _aiTestConnectionMessage = string.Empty;

    // --- Acerca de (no persistence) ---

    /// <summary>Gets the informational version of the application assembly.</summary>
    public string AppVersion { get; }

    /// <summary>Gets the localized "Version {0}" label shown in the Acerca de section.</summary>
    public string AppVersionLabel => string.Format(CultureInfo.InvariantCulture, Strings.SettingsAboutVersionLabel, AppVersion);

    // --- shared chrome, BudgetsViewModel's shape ---

    /// <summary>Gets or sets the error message shown when a save or load operation fails.</summary>
    [ObservableProperty]
    private string _formError = string.Empty;

    /// <summary>Gets or sets the status message shown after a successful operation.</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>Gets or sets a value indicating whether the initial load is in progress.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Gets the theme values available in the Apariencia picker.</summary>
    public IReadOnlyList<AppTheme> Themes { get; }

    /// <summary>Gets the locale values available in the Idioma picker.</summary>
    public IReadOnlyList<string> Locales { get; }

    /// <summary>Gets the AI provider values available in the IA picker.</summary>
    public IReadOnlyList<AiProvider> AiProviders { get; }

    /// <summary>
    /// Gets a value indicating whether the cleartext-API-key warning is
    /// visible. Always <see langword="true"/> — the warning is NOT
    /// conditional on <see cref="AiApiKey"/> having a value (R-5, S-8):
    /// the cleartext-storage risk exists whether or not a key has been
    /// entered yet.
    /// </summary>
    public bool IsAiApiKeyWarningVisible => true;

    /// <summary>
    /// Gets a value indicating whether <see cref="TestConnectionCommand"/>
    /// can execute: never while a previous run is still in flight (F-013,
    /// slice 7, R-8).
    /// </summary>
    public bool CanTestConnection => !IsTestingConnection;

    /// <summary>
    /// Gets the localized (value, label) projection of <see cref="Themes"/>
    /// the theme <c>ComboBox</c> actually binds to, so the picker renders
    /// <see cref="Resources.Strings.SettingsThemeDark"/> /
    /// <see cref="Resources.Strings.SettingsThemeLight"/> instead of the
    /// enum's raw name (D-12).
    /// </summary>
    public IReadOnlyList<ThemeOption> ThemeOptions { get; }

    /// <summary>
    /// Gets the localized (value, label) projection of <see cref="Locales"/>
    /// the language <c>ComboBox</c> actually binds to, so the picker
    /// renders <see cref="Resources.Strings.SettingsLanguageSpanish"/> /
    /// <see cref="Resources.Strings.SettingsLanguageEnglish"/> instead of
    /// the raw locale code (D-12).
    /// </summary>
    public IReadOnlyList<LocaleOption> LocaleOptions { get; }

    /// <summary>
    /// Persists <see cref="Theme"/> via <see cref="UpdateThemeUseCase"/>
    /// (R-3). Never touches <see cref="IsRestartRequired"/> or any
    /// Avalonia theme-application API (D-8).
    /// </summary>
    [RelayCommand]
    private async Task SaveThemeAsync()
    {
        FormError = string.Empty;
        StatusMessage = string.Empty;

        Result result = await _updateThemeUseCase.ExecuteAsync(Theme).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            FormError = result.Error?.Message ?? Strings.SettingsErrorMessage;
            return;
        }

        StatusMessage = Strings.SettingsSavedMessage;
    }

    /// <summary>
    /// Persists <see cref="Locale"/> via <see cref="UpdateLocaleUseCase"/>
    /// (R-4) and, only on success, sets <see cref="IsRestartRequired"/> so
    /// the view can show the restart-required notice (Q2).
    /// </summary>
    [RelayCommand]
    private async Task SaveLocaleAsync()
    {
        FormError = string.Empty;
        StatusMessage = string.Empty;

        Result result = await _updateLocaleUseCase.ExecuteAsync(Locale).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            FormError = result.Error?.Message ?? Strings.SettingsErrorMessage;
            return;
        }

        StatusMessage = Strings.SettingsSavedMessage;
        IsRestartRequired = true;
    }

    /// <summary>
    /// Builds an <see cref="UpdateAiSettingsCommand"/> verbatim from the
    /// six IA form fields and persists it via
    /// <see cref="UpdateAiSettingsUseCase"/> (R-5, D-10). Never touches
    /// <see cref="IsRestartRequired"/> — the AI section has no post-save
    /// affordance beyond the status message.
    /// </summary>
    [RelayCommand]
    private async Task SaveAiAsync()
    {
        FormError = string.Empty;
        StatusMessage = string.Empty;

        var command = new UpdateAiSettingsCommand(
            AiProvider,
            AiApiKey,
            AiModel,
            AiBaseUrl,
            AiTemperature,
            AiMaxTokensPerRequest);

        Result result = await _updateAiSettingsUseCase.ExecuteAsync(command).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            FormError = result.Error?.Message ?? Strings.SettingsErrorMessage;
            return;
        }

        StatusMessage = Strings.SettingsSavedMessage;
    }

    /// <summary>
    /// Runs the "Probar conexión" diagnostic against the SAVED (persisted)
    /// AI configuration via <see cref="TestAiConnectionUseCase"/> — never
    /// against unsaved form field values (F-013, slice 7, R-8, D-11).
    /// Never writes <see cref="FormError"/>: a connection-test result is
    /// information, not a form error, and reusing <see cref="FormError"/>
    /// would leave a red banner under an unrelated save button.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanTestConnection))]
    private async Task TestConnectionAsync()
    {
        IsTestingConnection = true;
        TestConnectionCommand.NotifyCanExecuteChanged();
        AiTestConnectionMessage = Strings.SettingsAiTestConnectionTesting;

        try
        {
            Result result = await _testAiConnectionUseCase.ExecuteAsync().ConfigureAwait(false);
            AiTestConnectionMessage = result.IsSuccess
                ? MessageForOutcome((TestAiConnectionDto)result.Value!)
                : Strings.SettingsAiTestConnectionUnavailable;
        }
        catch (Exception ex)
        {
            // Defense in depth: TestAiConnectionUseCase.ExecuteAsync is
            // documented to never throw (D-11), but no exception may ever
            // reach the UI (roadmap criterion b's rule, applied here too).
            _logger.LogWarning(ex, "AI connection test failed unexpectedly.");
            AiTestConnectionMessage = Strings.SettingsAiTestConnectionUnavailable;
        }
        finally
        {
            IsTestingConnection = false;
            TestConnectionCommand.NotifyCanExecuteChanged();
        }
    }

    private static string MessageForOutcome(TestAiConnectionDto dto) => dto.Kind switch
    {
        AiConnectionResultKind.Ok => string.Format(CultureInfo.InvariantCulture, Strings.SettingsAiTestConnectionOk, dto.ProviderName),
        AiConnectionResultKind.NotConfigured => Strings.SettingsAiTestConnectionNotConfigured,
        AiConnectionResultKind.InvalidCredentials => Strings.SettingsAiTestConnectionInvalidCredentials,
        AiConnectionResultKind.Unavailable => Strings.SettingsAiTestConnectionUnavailable,
        _ => Strings.SettingsAiTestConnectionUnavailable,
    };

    private async Task LoadAsync()
    {
        IsLoading = true;
        FormError = string.Empty;

        try
        {
            Result result = await _getAppSettingsQuery.ExecuteAsync().ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                FormError = result.Error?.Message ?? Strings.SettingsLoadErrorMessage;
                return;
            }

            var settings = (AppSettings)result.Value!;
            Theme = settings.Theme;
            Locale = settings.Locale;
            AiProvider = settings.AiProvider;
            AiApiKey = settings.AiApiKey;
            AiModel = settings.AiModel;
            AiBaseUrl = settings.AiBaseUrl;
            AiTemperature = settings.AiTemperature;
            AiMaxTokensPerRequest = settings.AiMaxTokensPerRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load settings.");
            FormError = Strings.SettingsLoadErrorMessage;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string ReadInformationalVersion()
        => typeof(SettingsViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? "0.0.0";

    private static string DisplayNameForTheme(AppTheme theme)
        => theme switch
        {
            AppTheme.Dark => Strings.SettingsThemeDark,
            AppTheme.Light => Strings.SettingsThemeLight,
            _ => theme.ToString(),
        };

    private static string DisplayNameForLocale(string locale)
        => locale switch
        {
            SupportedLocales.English => Strings.SettingsLanguageEnglish,
            SupportedLocales.SpanishDominican => Strings.SettingsLanguageSpanish,
            _ => locale,
        };
}

/// <summary>
/// Localized (value, label) projection of an <see cref="AppTheme"/> value
/// for the Apariencia <c>ComboBox</c> (D-12).
/// </summary>
public sealed record ThemeOption(AppTheme Value, string Label);

/// <summary>
/// Localized (value, label) projection of a supported locale code for the
/// Idioma <c>ComboBox</c> (D-12).
/// </summary>
public sealed record LocaleOption(string Value, string Label);
