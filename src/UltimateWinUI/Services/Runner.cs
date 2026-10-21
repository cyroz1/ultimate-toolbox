using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UltimateWinUI.Models;

namespace UltimateWinUI.Services;

/// <summary>
/// Runs tweak options without ever showing a console:
///
///   apply/check options -> the option's script body (extracted verbatim from
///     the upstream script by the catalog generator) is written to a temp .ps1
///     and run with Windows PowerShell, hidden, output piped to the log view.
///     The app itself runs elevated (see app.manifest), so children inherit
///     admin rights and the scripts never relaunch into a visible console.
///
///   launch options -> opened through the shell (ms-settings pages, control
///     panel applets, websites, other apps).
/// </summary>
public sealed class Runner
{
    private readonly LogService _log;
    private readonly string _workDir;

    public Runner(LogService log)
    {
        _log = log;
        _workDir = Path.Combine(CatalogService.DataDir, "work");
        Directory.CreateDirectory(_workDir);
    }

    public async Task<int> RunOptionAsync(Tweak tweak, TweakOption option, IReadOnlyList<string>? inputs = null)
    {
        if (option.Kind == "launch")
        {
            Launch(option);
            _log.Write($"{tweak.Name}: opened {option.Target}");
            return 0;
        }

        var fileName = $"{Sanitize(tweak.Id)}-{Sanitize(option.Label)}.ps1";
        var path = Path.Combine(_workDir, fileName);
        var script = "$ErrorActionPreference = 'Continue'\n" +
                     "$ProgressPreference = 'SilentlyContinue'\n" +
                     option.Body;
        await File.WriteAllTextAsync(path, script);

        _log.Write($"--- {tweak.Name} -> {option.Label} ---");
        var exit = await RunPowerShellHiddenAsync(path, inputs);
        _log.Write($"--- {tweak.Name} finished (exit {exit}) ---");
        return exit;
    }

    public void Launch(TweakOption option)
    {
        var target = option.Target;
        if (string.IsNullOrWhiteSpace(target))
        {
            // Fall back to running the body; it contains the Start-Process call.
            if (!string.IsNullOrWhiteSpace(option.Body))
                _ = RunInlineAsync(option.Body);
            return;
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true,
        });
    }

    public async Task CreateRestorePointAsync()
    {
        _log.Write("Creating restore point (UltimateWinUI)...");
        var path = Path.Combine(_workDir, "restore-point.ps1");
        await File.WriteAllTextAsync(path,
            "Enable-ComputerRestore -Drive 'C:\\' -ErrorAction SilentlyContinue\n" +
            "Checkpoint-Computer -Description 'UltimateWinUI' -RestorePointType 'MODIFY_SETTINGS'\n");
        var exit = await RunPowerShellHiddenAsync(path);
        _log.Write(exit == 0
            ? "Restore point created."
            : $"Restore point failed (exit {exit}). Continuing anyway; see log output above.");
    }

    private async Task RunInlineAsync(string body)
    {
        var path = Path.Combine(_workDir, $"inline-{Guid.NewGuid():N}.ps1");
        await File.WriteAllTextAsync(path, body);
        await RunPowerShellHiddenAsync(path);
    }

    private Task<int> RunPowerShellHiddenAsync(string scriptPath, IReadOnlyList<string>? inputs = null)
    {
        var tcs = new TaskCompletionSource<int>();
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = inputs != null,
        };
        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) _log.Write(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) _log.Write("ERR: " + e.Data); };
        proc.Exited += (_, _) =>
        {
            tcs.TrySetResult(proc.ExitCode);
            proc.Dispose();
        };
        try
        {
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            if (inputs != null)
            {
                // Feed the script's Read-Host prompts, then close stdin so any
                // stray prompt gets EOF instead of hanging forever.
                foreach (var line in inputs)
                    proc.StandardInput.WriteLine(line);
                proc.StandardInput.Close();
            }
        }
        catch (Exception ex)
        {
            _log.Write("Failed to start PowerShell: " + ex.Message);
            tcs.TrySetResult(-1);
        }
        return tcs.Task;
    }

    private static string Sanitize(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_')
                chars[i] = '-';
        var s = new string(chars);
        return s.Length > 60 ? s[..60] : s;
    }
}
