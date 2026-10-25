using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace UltimateWinUI.Services;

/// <summary>
/// In-app log. PowerShell output lands here instead of a console window;
/// the same lines are appended to a log file in the data dir.
/// </summary>
public sealed class LogService
{
    private const int MaxLinesInMemory = 2000;
    private readonly DispatcherQueue? _dispatcher;
    private readonly string _logFile;
    private readonly ConcurrentQueue<string> _pendingUiLines = new();
    private readonly ConcurrentQueue<string> _pendingFileLines = new();
    private int _uiFlushScheduled;
    private int _fileFlushScheduled;

    public ObservableCollection<string> Lines { get; } = new();

    public LogService()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        var logDir = Path.Combine(CatalogService.DataDir, "logs");
        Directory.CreateDirectory(logDir);
        _logFile = Path.Combine(logDir, $"app-{DateTime.Now:yyyyMMdd}.log");
    }

    public string LogFilePath => _logFile;

    public void Write(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        QueueFileWrite(line);

        if (_dispatcher == null || _dispatcher.HasThreadAccess)
            Append(line);
        else
            QueueUiAppend(line);
    }

    private void QueueFileWrite(string line)
    {
        _pendingFileLines.Enqueue(line);
        if (Interlocked.Exchange(ref _fileFlushScheduled, 1) == 0)
            _ = Task.Run(FlushFileLinesAsync);
    }

    private async Task FlushFileLinesAsync()
    {
        while (true)
        {
            // Give concurrent process-output callbacks a short window to join
            // the same file write instead of opening the log once per line.
            await Task.Delay(50).ConfigureAwait(false);

            var batch = new StringBuilder();
            while (_pendingFileLines.TryDequeue(out var line))
                batch.AppendLine(line);

            if (batch.Length > 0)
            {
                try
                {
                    await File.AppendAllTextAsync(_logFile, batch.ToString()).ConfigureAwait(false);
                }
                catch { /* logging must not interrupt a running script */ }
            }

            Interlocked.Exchange(ref _fileFlushScheduled, 0);
            if (_pendingFileLines.IsEmpty || Interlocked.Exchange(ref _fileFlushScheduled, 1) != 0)
                return;
        }
    }

    private void QueueUiAppend(string line)
    {
        _pendingUiLines.Enqueue(line);
        if (Interlocked.Exchange(ref _uiFlushScheduled, 1) == 0 &&
            !_dispatcher!.TryEnqueue(FlushUiLines))
            Interlocked.Exchange(ref _uiFlushScheduled, 0);
    }

    private void FlushUiLines()
    {
        while (true)
        {
            while (_pendingUiLines.TryDequeue(out var line))
                Append(line);

            Interlocked.Exchange(ref _uiFlushScheduled, 0);
            if (_pendingUiLines.IsEmpty || Interlocked.Exchange(ref _uiFlushScheduled, 1) != 0)
                return;
        }
    }

    private void Append(string line)
    {
        Lines.Add(line);
        while (Lines.Count > MaxLinesInMemory)
            Lines.RemoveAt(0);
    }

    public void Clear() => Lines.Clear();
}
