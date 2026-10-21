using System;
using Microsoft.Win32;
using UltimateWinUI.Models;

namespace UltimateWinUI.Services;

/// <summary>
/// Live state detection. For every tweak whose options carry registry
/// signatures (extracted from the scripts by tools/catalog_gen.py), the
/// detector reads the current registry values and scores each option:
///
///   - an option matches only when every signature value can be read and
///     equals the live value (a "deleted" entry matches when the value is
///     confirmed absent)
///   - the option with a full match and the most signature entries wins
///   - tied full matches are ambiguous and are not reported as detected
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
        var bestIsAmbiguous = false;

        foreach (var option in tweak.ChoiceOptions)
        {
            var entries = 0;
            var matched = 0;

            if (option.Signature != null)
                foreach (var sig in option.Signature)
                {
                    entries++;
                    if (sig != null && ValueMatches(sig)) matched++;
                }
            if (option.Deletes != null)
                foreach (var del in option.Deletes)
                {
                    entries++;
                    if (del != null && ReadValue(del.Key, del.Name).State == RegistryReadState.Missing)
                        matched++;
                }

            if (entries == 0 || matched != entries)
                continue;

            if (entries > bestScore)
            {
                best = option;
                bestScore = entries;
                bestIsAmbiguous = false;
            }
            else if (entries == bestScore)
            {
                bestIsAmbiguous = true;
            }
        }

        if (best != null && !bestIsAmbiguous)
        {
            tweak.CurrentOptionLabel = best.Label;
            tweak.CurrentSource = StateSource.Detected;
            return;
        }

        var record = store.Get(tweak.Id);
        if (record != null && tweak.ChoiceOptions.Exists(o =>
                string.Equals(o.Label, record.OptionLabel, StringComparison.Ordinal)))
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
        var result = ReadValue(sig.Key, sig.Name);
        if (sig.Kind == "deleted") return result.State == RegistryReadState.Missing;
        if (result.State != RegistryReadState.Found) return false;

        if (sig.Kind == "dword")
        {
            var expected = ToDword(sig.Value);
            var actual = ToDword(result.Value);
            return expected.HasValue && actual.HasValue && expected == actual;
        }
        if (sig.Kind == "qword")
        {
            var expected = ToQword(sig.Value);
            var actual = ToQword(result.Value);
            return expected.HasValue && actual.HasValue && expected == actual;
        }

        if (sig.Kind == "binary" && result.Value is byte[] bytes)
            return string.Equals(Convert.ToHexString(bytes), ToStringValue(sig.Value), StringComparison.OrdinalIgnoreCase);

        return sig.Kind == "string" && result.Value is string live &&
               string.Equals(live, ToStringValue(sig.Value), StringComparison.OrdinalIgnoreCase);
    }

    private static uint? ToDword(object? value) => value switch
    {
        null => null,
        int i => unchecked((uint)i),
        uint u => u,
        System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetUInt32(out var n) => n,
        _ => null,
    };

    private static ulong? ToQword(object? value) => value switch
    {
        null => null,
        long l => unchecked((ulong)l),
        ulong u => u,
        System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetUInt64(out var n) => n,
        _ => null,
    };

    private static string? ToStringValue(object? value) => value switch
    {
        string s => s,
        System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.String => je.GetString(),
        _ => null,
    };

    private enum RegistryReadState
    {
        Missing,
        Found,
        Unavailable,
    }

    private readonly record struct RegistryReadResult(RegistryReadState State, object? Value = null);

    private static RegistryReadResult ReadValue(string fullKeyPath, string valueName)
    {
        try
        {
            if (!TrySplitKey(fullKeyPath, out var hive, out var subKey))
                return new RegistryReadResult(RegistryReadState.Unavailable);

            using var key = hive.OpenSubKey(subKey, writable: false);
            if (key == null)
                return new RegistryReadResult(RegistryReadState.Missing);

            var valueExists = Array.Exists(key.GetValueNames(), name =>
                string.Equals(name, valueName, StringComparison.OrdinalIgnoreCase));
            if (!valueExists)
                return new RegistryReadResult(RegistryReadState.Missing);

            var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            return value == null
                ? new RegistryReadResult(RegistryReadState.Unavailable)
                : new RegistryReadResult(RegistryReadState.Found, value);
        }
        catch
        {
            return new RegistryReadResult(RegistryReadState.Unavailable);
        }
    }

    private static bool TrySplitKey(string path, out RegistryKey hive, out string subKey)
    {
        var normalized = path.Trim();
        if (normalized.StartsWith("Registry::", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["Registry::".Length..];

        var separator = normalized.IndexOf('\\');
        var root = separator >= 0 ? normalized[..separator] : normalized;
        if (root.EndsWith(':'))
            root = root[..^1];
        subKey = separator >= 0 ? normalized[(separator + 1)..] : "";

        var matchedHive = root.ToUpperInvariant() switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKCR" or "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
            "HKU" or "HKEY_USERS" => Registry.Users,
            "HKCC" or "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
            _ => null,
        };

        if (matchedHive == null)
        {
            hive = null!;
            subKey = "";
            return false;
        }

        hive = matchedHive;
        return true;
    }
}
