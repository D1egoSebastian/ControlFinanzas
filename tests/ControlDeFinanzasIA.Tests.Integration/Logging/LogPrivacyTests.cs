using System.Net;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Settings.TestAiConnection;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateAiSettings;
using ControlDeFinanzasIA.Infrastructure.Llm;
using ControlDeFinanzasIA.Tests.Integration.TestDoubles;
using ControlDeFinanzasIA.UI.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Integration.Logging;

/// <summary>
/// F-021 R-5 / T1: the API key never reaches a log file. Runs the REAL composition root (real provider,
/// real SQLite in a temp dir, real use cases) and scans every file under <c>logs/</c> afterwards. A second
/// Trace-level in-memory provider proves the key is absent at every level, not only the ones the file keeps.
/// </summary>
[Collection(StderrCollection.Name)]
public sealed class LogPrivacyTests : IDisposable
{
    private const string _sentinel = "sk-SENTINEL-9f3a";
    private const string _envVar = "CONTROLDEFINANZASIA_DATA_DIR";

    private readonly TempDir _temp = new();
    private readonly string? _previousEnv = Environment.GetEnvironmentVariable(_envVar);
    private readonly CaptureProvider _capture = new();

    public LogPrivacyTests()
    {
        Environment.SetEnvironmentVariable(_envVar, _temp.Path);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(_envVar, _previousEnv);
        _temp.Dispose();
    }

    [Fact]
    public async Task SavingAiSettingsAndTestingTheConnection_NeverLogTheKey()
    {
        using (ServiceProvider root = BuildRoot())
        {
            using IServiceScope scope = root.CreateScope();
            IServiceProvider sp = scope.ServiceProvider;

            Result saved = await sp.GetRequiredService<UpdateAiSettingsUseCase>().ExecuteAsync(
                new UpdateAiSettingsCommand(AiProvider.Custom, _sentinel, "some-model", "http://127.0.0.1:9/v1", 0.2, 256));
            Assert.True(saved.IsSuccess, saved.Error?.Message);

            // Invalid command: exercises the validation-failure log path too.
            _ = await sp.GetRequiredService<UpdateAiSettingsUseCase>().ExecuteAsync(
                new UpdateAiSettingsCommand(AiProvider.Custom, _sentinel, string.Empty, "not a url", 99, -1));

            // Real connection attempt against a closed local port: real resilience pipeline, real failure.
            _ = await sp.GetRequiredService<TestAiConnectionUseCase>().ExecuteAsync();
        }

        AssertNoSentinelAnywhere();
        Assert.True(LogFileCount() > 0, "the real provider must have written something");
    }

    [Fact]
    public async Task ALlmCallThatFailsWith401_ThroughTheRealNamedHttpClientPipeline_NeverLogsTheKeyOrTheAuthorizationValue()
    {
        var fake = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized, "{\"error\":\"bad key\"}");
        using (ServiceProvider root = BuildRoot(services =>
        {
            // Only the PRIMARY handler is stubbed: the real logging + resilience handlers stay in the chain.
            foreach (string name in new[] { "llm-OpenAI", "llm-OpenAI-diagnostic" })
            {
                services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => fake);
            }
        }))
        {
            var settings = new AppSettings(
                AppTheme.Dark,
                SupportedLocales.Default,
                AiProvider.OpenAI,
                _sentinel,
                "gpt-test",
                "https://api.openai.test/v1",
                AppSettings.DefaultAiTemperature,
                AppSettings.DefaultAiMaxTokensPerRequest);
            ILLMClientFactory factory = root.GetRequiredService<ILLMClientFactory>();
            ILLMClient client = factory.Resolve(settings).Client!;
            ILLMClient diagnostic = factory.ResolveDiagnostic(settings).Client!;

            _ = await Record.ExceptionAsync(() => client.CompleteAsync(new LlmRequest
            {
                Messages = [new LlmMessage(LlmRole.User, "hello")],
                Temperature = 0.0,
                MaxTokens = 16,
            }));
            _ = await Record.ExceptionAsync(() => diagnostic.TestConnectionAsync());
        }

        // Not vacuous: the stub was reached through the pipeline, and the built-in HttpClient logging handler ran.
        Assert.NotEmpty(fake.Requests);
        Assert.Contains(fake.Requests, r => r.Headers.Any(h => h.Value.Contains(_sentinel, StringComparison.Ordinal)));
        Assert.Contains(_capture.Entries, e => e.Contains("System.Net.Http.HttpClient.llm-OpenAI", StringComparison.Ordinal));
        AssertNoSentinelAnywhere();
    }

    [Fact]
    public async Task AUserBackupExport_NeverLogsTheKey()
    {
        using (ServiceProvider root = BuildRoot())
        {
            using IServiceScope scope = root.CreateScope();
            _ = await scope.ServiceProvider.GetRequiredService<UpdateAiSettingsUseCase>().ExecuteAsync(
                new UpdateAiSettingsCommand(AiProvider.OpenAI, _sentinel, "m", null, 0.2, 256));

            var buffer = new MemoryStream();
            Result result = await scope.ServiceProvider.GetRequiredService<IBackupService>()
                .CreateAsync(_ => Task.FromResult<Stream>(buffer));

            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.True(buffer.ToArray().Length > 0);
        }

        AssertNoSentinelAnywhere();
    }

    private ServiceProvider BuildRoot(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.SetMinimumLevel(LogLevel.Trace);
            b.AddProvider(_capture);
        });
        Bootstrap.ConfigureServices(services, new ConfigurationBuilder().Build());
        configure?.Invoke(services);
        ServiceProvider root = services.BuildServiceProvider();
        Bootstrap.ApplyMigrations(root);
        return root;
    }

    private int LogFileCount()
    {
        string logs = Path.Combine(_temp.Path, "logs");
        return Directory.Exists(logs) ? Directory.GetFiles(logs).Length : 0;
    }

    private void AssertNoSentinelAnywhere()
    {
        string logs = Path.Combine(_temp.Path, "logs");
        foreach (string file in Directory.GetFiles(logs))
        {
            string text = File.ReadAllText(file);
            Assert.DoesNotContain(_sentinel, text, StringComparison.Ordinal);
            Assert.DoesNotContain("Bearer", text, StringComparison.OrdinalIgnoreCase);
        }

        string[] leaks = _capture.Entries
            .Where(e => e.Contains(_sentinel, StringComparison.Ordinal))
            .Select(e => e[..Math.Min(160, e.Length)])
            .ToArray();
        Assert.Equal(Array.Empty<string>(), leaks);
        Assert.NotEmpty(_capture.Entries);
    }

    private sealed class CaptureProvider : ILoggerProvider
    {
        private readonly object _gate = new();
        private readonly List<string> _entries = [];

        public IReadOnlyList<string> Entries
        {
            get
            {
                lock (_gate)
                {
                    return _entries.ToArray();
                }
            }
        }

        public string AllText => string.Join('\n', Entries);

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this, categoryName);

        public void Dispose()
        {
        }

        private void Add(string text)
        {
            lock (_gate)
            {
                _entries.Add(text);
            }
        }

        private sealed class CaptureLogger(CaptureProvider owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner.Add(logLevel + " " + category + ": " + formatter(state, exception) + (exception is null ? string.Empty : "\n" + exception));
        }
    }
}
