using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UltimateWinUI.Models;

namespace UltimateWinUI.Services;

public static class CatalogService
{
    public static CatalogDoc? Catalog { get; private set; }

    public static CatalogDoc Load()
    {
        if (Catalog != null) return Catalog;
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "tweaks.json");
        var json = File.ReadAllText(path);
        var doc = JsonSerializer.Deserialize<CatalogDoc>(json)
                  ?? throw new InvalidOperationException("tweaks.json could not be parsed.");
        foreach (var cat in doc.Categories)
            foreach (var tweak in cat.Tweaks)
                tweak.Parent = cat;
        Catalog = doc;
        return doc;
    }

    public static string DataDir
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UltimateWinUI");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}

/// <summary>Small user settings, persisted as JSON in the data dir.</summary>
public sealed class AppSettings
{
    public bool CreateRestorePointBeforeApply { get; set; } = true;
    public bool ConfirmAdvanced { get; set; } = true;
    public string Theme { get; set; } = "System"; // System | Light | Dark

    private static string FilePath => Path.Combine(CatalogService.DataDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { /* fall through to defaults */ }
        return new AppSettings();
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
        catch { /* non-fatal */ }
    }
}

/// <summary>
/// Remembers which option the app last applied per tweak. This is the fallback
/// state source for tweaks whose state cannot be read back from the registry,
/// and it is what survives restarts.
/// </summary>
public sealed class StateStore
{
    public sealed class Record
    {
        public string OptionLabel { get; set; } = "";
        public DateTimeOffset AppliedUtc { get; set; }
    }

    private readonly Dictionary<string, Record> _records;
    private static string FilePath => Path.Combine(CatalogService.DataDir, "state.json");

    public StateStore()
    {
        _records = new Dictionary<string, Record>();
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, Record>>(File.ReadAllText(FilePath));
                if (loaded != null) _records = loaded;
            }
        }
        catch { /* start empty */ }
    }

    public Record? Get(string tweakId) => _records.TryGetValue(tweakId, out var r) ? r : null;

    public void Set(string tweakId, string optionLabel)
    {
        _records[tweakId] = new Record { OptionLabel = optionLabel, AppliedUtc = DateTimeOffset.UtcNow };
        Save();
    }

    private void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(_records, new JsonSerializerOptions { WriteIndented = true })); }
        catch { /* non-fatal */ }
    }
}
