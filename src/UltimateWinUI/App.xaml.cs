using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UltimateWinUI.Models;
using UltimateWinUI.Services;

namespace UltimateWinUI;

public partial class App : Application
{
    public static CatalogDoc Catalog { get; private set; } = null!;
    public static AppSettings Settings { get; private set; } = null!;
    public static StateStore Store { get; private set; } = null!;
    public static LogService Log { get; private set; } = null!;
    public static Runner Runner { get; private set; } = null!;
    public static MainWindow Window { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Settings = AppSettings.Load();
        Catalog = CatalogService.Load();
        Store = new StateStore();
        Log = new LogService();
        Runner = new Runner(Log);

        Log.Write($"UltimateWinUI started. {CountTweaks()} tweaks loaded from the FR33THY Ultimate library.");

        // Detect current states off the UI thread, then refresh whatever is shown.
        _ = Task.Run(() =>
        {
            Detector.DetectAll(Catalog, Store);
            Log.Write("State detection finished.");
            Window?.DispatcherQueue.TryEnqueue(() => Window.RefreshCurrentPage());
        });

        Window = new MainWindow();
        Window.Activate();
        Window.ApplyTheme(Settings.Theme);
        Window.NavigateStartupPage();
    }

    private static int CountTweaks()
    {
        var n = 0;
        foreach (var c in Catalog.Categories) n += c.Tweaks.Count;
        return n;
    }

    /// <summary>
    /// Shared apply pipeline used by every tweak control: optional Advanced
    /// confirmation, optional restore point, hidden run, state save, re-detect.
    /// </summary>
    public static async Task<bool> ApplyOptionAsync(Tweak tweak, TweakOption option, System.Collections.Generic.IReadOnlyList<string>? inputs = null)
    {
        if (tweak.Risk == "high" && Settings.ConfirmAdvanced && option.Kind == "apply")
        {
            var dialog = new ContentDialog
            {
                Title = "High risk tweak",
                Content = $"\"{tweak.Name}\" is in the Advanced category. These tweaks can disable " +
                          "security features or change low level system behavior.\n\n" +
                          $"Run \"{option.Label}\"?",
                PrimaryButtonText = "Run",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Window.Content.XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return false;
        }

        tweak.IsBusy = true;
        Window.RefreshCurrentPage();
        try
        {
            if (Settings.CreateRestorePointBeforeApply && option.Kind == "apply")
                await Runner.CreateRestorePointAsync();

            var exit = await Runner.RunOptionAsync(tweak, option, inputs);
            if (option.Kind == "apply")
            {
                Store.Set(tweak.Id, option.Label);
                Detector.Detect(tweak, Store);
            }
            Window.ShowInfo(
                exit == 0 ? $"{tweak.Name}: {option.Label}" : $"{tweak.Name} finished with exit code {exit}",
                exit == 0 ? "Done. Some tweaks only take full effect after a restart."
                          : "Done, but the script reported errors. Open Logs for details.",
                exit == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
            return exit == 0;
        }
        finally
        {
            tweak.IsBusy = false;
            Window.RefreshCurrentPage();
        }
    }
}
