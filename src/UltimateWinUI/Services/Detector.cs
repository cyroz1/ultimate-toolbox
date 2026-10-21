using System;
using Microsoft.Win32;
using UltimateWinUI.Models;

namespace UltimateWinUI.Services;

/// <summary>
/// Live state detection. For every tweak whose options carry registry
/// signatures (extracted from the scripts by tools/catalog_gen.py), the
/// detector reads the current registry values and scores each option:
///
///   - an option matches when every signature value equals the live value
///     (a "deleted" entry matches when the value is absent)
///   - the option with a full match and the most signature entries wins
///
/// Tweaks without a full match keep whatever the StateStore last applied,
/// or stay Unknown.
/// </summary>
public static class Detector
{
    public static void Detect(Tweak tweak, StateStore store)
    {
        TweakOption? best = null;
        var bestScore = 0;

        foreach (var option in tweak.ChoiceOptions)
        {
            var entries = 0;
            var matched = 0;

            if (option.Signature != null)
                foreach (var sig in option.Signature)
                {
                    entries++;
                    if (ValueMatches(sig)) matched++;
                }
            if (option.Deletes != null)
                foreach (var del in option.Deletes)
                {
                    entries++;
                    if (ReadValue(del.Key, del.Name) == null) matched++;
                }

            if (entries > 0 && matched == entries && entries > bestScore)
            {
                best = option;
                bestScore = entries;
            }
        }

        if (best != null)
        {
            tweak.CurrentOptionLabel = best.Label;
            tweak.CurrentSource = StateSource.Detected;
            return;
        }

        var record = store.Get(tweak.Id);
        if (record != null)
        {
            tweak.CurrentOptionLabel = record.OptionLabel;
            tweak.CurrentSource = StateSource.Applied;
        }
        else
        {
            tweak.CurrentOptionLabel = null;
            tweak.CurrentSource = StateSource.Unknown;
        }
    }

    public static void DetectAll(CatalogDoc catalog, StateStore store)
    {
        foreach (var cat in catalog.Categories)
            foreach (var tweak in cat.Tweaks)
                Detect(tweak, store);
    }

    private static bool ValueMatches(SigEntry sig)
    {
        var live = ReadValue(sig.Key, sig.Name);
        if (sig.Kind == "deleted") return live == null;
        if (live == null) return false;
        if (sig.Kind == "dword")
        {
            var expected = ToLong(sig.Value);
            var actual = ToLong(live);
            return expected.HasValue && actual.HasValue && expected == actual;
        }
        return string.Equals(live.ToString(), sig.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static long? ToLong(object? value) => value switch
    {
        null => null,
        int i => i,
        long l => l,
        uint u => u,
        System.Text.Json.JsonElement je when je.TryGetInt64(out var n) => n,
        _ => long.TryParse(value.ToString(), out var p) ? p : null,
    };

    private static object? ReadValue(string fullKeyPath, string valueName)
    {
        try
        {
            var (hive, subKey) = SplitKey(fullKeyPath);
            using var key = hive.OpenSubKey(subKey, writable: false);
            return key?.GetValue(valueName);
        }
        catch
        {
            return null;
        }
    }

    private static (RegistryKey Hive, string SubKey) SplitKey(string path)
    {
        var idx = path.IndexOf('\\');
        var root = idx >= 0 ? path[..idx] : path;
        var sub = idx >= 0 ? path[(idx + 1)..] : "";
        var hive = root.ToUpperInvariant() switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKCR" or "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
            "HKU" or "HKEY_USERS" => Registry.Users,
            "HKCC" or "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
            _ => Registry.LocalMachine,
        };
        return (hive, sub);
    }
}
