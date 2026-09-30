using Avalonia.Threading;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.UI.System;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.Diagnostics;

/// <summary>
/// Installs the three process-wide exception hooks (F-021, DD-2): the Avalonia UI-thread hook (reports and keeps the
/// app alive, behind a breaker), the AppDomain hook (Critical, log only) and the unobserved-task hook (Warning,
/// log only). No hook ever throws.
/// </summary>
public static class GlobalExceptionHooks
{
    /// <summary>Installs the hooks with the system clock.</summary>
    /// <param name="reporter">The reporter all hooks forward to.</param>
    /// <returns>A handle that removes the hooks when disposed.</returns>
    public static IDisposable Install(IUnexpectedErrorReporter reporter) => Install(reporter, new SystemClock());

    /// <summary>Installs the hooks with an explicit clock (breaker window).</summary>
    /// <param name="reporter">The reporter all hooks forward to.</param>
    /// <param name="clock">Time source for the breaker.</param>
    /// <returns>A handle that removes the hooks when disposed.</returns>
    public static IDisposable Install(IUnexpectedErrorReporter reporter, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(reporter);
        ArgumentNullException.ThrowIfNull(clock);
        var subscription = new Subscription(reporter, clock);
        subscription.Attach();
        return subscription;
    }

    /// <summary>The installed hooks; the handler bodies are internal so tests can drive them directly.</summary>
    internal sealed class Subscription : IDisposable
    {
        internal const int BreakerLimit = 20;

        private static readonly TimeSpan _breakerWindow = TimeSpan.FromSeconds(10);

        private readonly IUnexpectedErrorReporter _reporter;
        private readonly IClock _clock;
        private readonly Queue<DateTime> _recent = new();
        private readonly object _sync = new();
        private bool _tripLogged;
        private bool _attached;

        internal Subscription(IUnexpectedErrorReporter reporter, IClock clock)
        {
            _reporter = reporter;
            _clock = clock;
        }

        internal void Attach()
        {
            Dispatcher.UIThread.UnhandledException += OnDispatcherException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTask;
            _attached = true;
        }

        public void Dispose()
        {
            if (!_attached)
            {
                return;
            }

            _attached = false;
            Dispatcher.UIThread.UnhandledException -= OnDispatcherException;
            AppDomain.CurrentDomain.UnhandledException -= OnDomainException;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTask;
        }

        /// <summary>Reports a UI-thread failure; returns <see langword="true"/> only when it may be marked handled.</summary>
        internal bool HandleUiException(Exception exception)
        {
            try
            {
                if (!TryAdmit())
                {
                    LogTrip(exception);
                    return false;
                }

                _reporter.Report(exception, "ui");
                return true;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return false;
            }
        }

        internal void HandleDomainException(object? exceptionObject)
        {
            Exception exception = exceptionObject as Exception
                ?? new InvalidOperationException("Non-exception object thrown: " + exceptionObject);
            SafeLogOnly(exception, "appdomain", LogLevel.Critical);
        }

        internal void HandleUnobservedTask(UnobservedTaskExceptionEventArgs args)
        {
            try
            {
                SafeLogOnly(args.Exception, "task", LogLevel.Warning);
            }
            finally
            {
                args.SetObserved();
            }
        }

        private void OnDispatcherException(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (HandleUiException(e.Exception))
            {
                e.Handled = true;
            }
        }

        private void OnDomainException(object? sender, UnhandledExceptionEventArgs e) =>
            HandleDomainException(e.ExceptionObject);

        private void OnUnobservedTask(object? sender, UnobservedTaskExceptionEventArgs e) =>
            HandleUnobservedTask(e);

        private bool TryAdmit()
        {
            DateTime now = _clock.UtcNow;
            lock (_sync)
            {
                while (_recent.Count > 0 && now - _recent.Peek() >= _breakerWindow)
                {
                    _recent.Dequeue();
                }

                if (_recent.Count >= BreakerLimit)
                {
                    return false;
                }

                _recent.Enqueue(now);
                _tripLogged = false;
                return true;
            }
        }

        private void LogTrip(Exception exception)
        {
            bool first;
            lock (_sync)
            {
                first = !_tripLogged;
                _tripLogged = true;
            }

            if (first)
            {
                SafeLogOnly(exception, "ui-breaker", LogLevel.Critical);
            }
        }

        private void SafeLogOnly(Exception exception, string source, LogLevel level)
        {
            try
            {
                _reporter.LogOnly(exception, source, level);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // A hook must never throw; the reporter already falls back to stderr on its own failures.
            }
        }
    }
}
