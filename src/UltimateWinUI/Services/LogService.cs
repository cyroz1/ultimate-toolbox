using System;
using System.Collections.ObjectModel;
using System.IO;
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
        try { File.AppendAllText(_logFile, line + Environment.NewLine); } catch { }
        if (_dispatcher != null && !_dispatcher.HasThreadAccess)
            _dispatcher.TryEnqueue(() => Append(line));
        else
            Append(line);
    }

    private void Append(string line)
    {
        Lines.Add(line);
        while (Lines.Count > MaxLinesInMemory)
            Lines.RemoveAt(0);
    }

    public void Clear() => Lines.Clear();
}
