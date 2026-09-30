using ControlDeFinanzasIA.Infrastructure.Backup;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>
/// Failure-injection decorator over <see cref="PhysicalBackupFileSystem"/> (F-019, task 4a.1). A fault
/// targets one named operation (optionally filtered by its path arguments and by occurrence), can run a
/// side effect before the operation, can throw instead of performing it, and can run a side effect after
/// the real operation succeeded. Every call is recorded in order.
/// </summary>
public sealed class FaultyBackupFileSystem : IBackupFileSystem
{
    private readonly PhysicalBackupFileSystem _inner = new();
    private readonly List<Fault> _faults = [];
    private readonly List<string> _calls = [];
    private readonly object _sync = new();

    /// <summary>Ordered call log, one entry per call: <c>Operation arg0 -> arg1</c>.</summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_sync)
            {
                return _calls.ToArray();
            }
        }
    }

    /// <summary>When set, replaces the free-space answer of the real file system.</summary>
    public long? FreeSpace { get; set; }

    /// <summary>Registers a fault for <paramref name="operation"/>; configure the returned object.</summary>
    /// <param name="operation">Method name: Create, Replace, Move, Delete, OpenRead, ...</param>
    /// <param name="match">Optional filter over the call arguments.</param>
    /// <param name="occurrence">1-based index among matching calls; 0 means every matching call.</param>
    public Fault On(string operation, Func<string[], bool>? match = null, int occurrence = 0)
    {
        var fault = new Fault(operation, match, occurrence);
        lock (_sync)
        {
            _faults.Add(fault);
        }

        return fault;
    }

    public int CountCalls(string operation) => Calls.Count(c => c.StartsWith(operation + " ", StringComparison.Ordinal) || c == operation);

    public bool FileExists(string path) => Run(nameof(FileExists), [path], () => _inner.FileExists(path));

    public long GetLength(string path) => Run(nameof(GetLength), [path], () => _inner.GetLength(path));

    public DateTime GetLastWriteTimeUtc(string path) => Run(nameof(GetLastWriteTimeUtc), [path], () => _inner.GetLastWriteTimeUtc(path));

    public long GetAvailableFreeSpace(string directory) =>
        Run(nameof(GetAvailableFreeSpace), [directory], () => FreeSpace ?? _inner.GetAvailableFreeSpace(directory));

    public Stream OpenRead(string path) => Run(nameof(OpenRead), [path], () => _inner.OpenRead(path));

    public Stream Create(string path) => Run(nameof(Create), [path], () => _inner.Create(path));

    public void Replace(string source, string destination, string backup) =>
        Run(nameof(Replace), [source, destination, backup], () =>
        {
            _inner.Replace(source, destination, backup);
            return true;
        });

    public void Move(string source, string destination) =>
        Run(nameof(Move), [source, destination], () =>
        {
            _inner.Move(source, destination);
            return true;
        });

    public void Delete(string path) =>
        Run(nameof(Delete), [path], () =>
        {
            _inner.Delete(path);
            return true;
        });

    public void CreateDirectory(string path) =>
        Run(nameof(CreateDirectory), [path], () =>
        {
            _inner.CreateDirectory(path);
            return true;
        });

    public void DeleteDirectory(string path) =>
        Run(nameof(DeleteDirectory), [path], () =>
        {
            _inner.DeleteDirectory(path);
            return true;
        });

    public IEnumerable<string> EnumerateFiles(string directory, string pattern) =>
        Run(nameof(EnumerateFiles), [directory, pattern], () => _inner.EnumerateFiles(directory, pattern).ToArray());

    private T Run<T>(string operation, string[] args, Func<T> real)
    {
        Fault? fault = null;
        lock (_sync)
        {
            _calls.Add(operation + " " + string.Join(" -> ", args));
            foreach (Fault candidate in _faults)
            {
                if (candidate.Applies(operation, args))
                {
                    fault = candidate;
                    break;
                }
            }
        }

        fault?.BeforeAction?.Invoke(args);
        if (fault?.Exception is not null)
        {
            throw fault.Exception;
        }

        T result = real();
        fault?.AfterAction?.Invoke(args);
        return result;
    }

    /// <summary>One configured fault.</summary>
    public sealed class Fault(string operation, Func<string[], bool>? match, int occurrence)
    {
        private int _matches;

        public Exception? Exception { get; private set; }

        public Action<string[]>? BeforeAction { get; private set; }

        public Action<string[]>? AfterAction { get; private set; }

        /// <summary>Throw <paramref name="exception"/> instead of performing the operation.</summary>
        public Fault Throws(Exception exception)
        {
            Exception = exception;
            return this;
        }

        /// <summary>Run <paramref name="action"/> before the operation (and before a configured throw).</summary>
        public Fault Before(Action<string[]> action)
        {
            BeforeAction = action;
            return this;
        }

        /// <summary>Run <paramref name="action"/> after the real operation succeeded.</summary>
        public Fault After(Action<string[]> action)
        {
            AfterAction = action;
            return this;
        }

        internal bool Applies(string name, string[] args)
        {
            if (!string.Equals(name, operation, StringComparison.Ordinal) || (match is not null && !match(args)))
            {
                return false;
            }

            _matches++;
            return occurrence == 0 || occurrence == _matches;
        }
    }
}
