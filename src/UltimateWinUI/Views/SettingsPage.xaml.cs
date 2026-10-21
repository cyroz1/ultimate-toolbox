using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UltimateWinUI.Services;

namespace UltimateWinUI.Views;

public sealed partial class SettingsPage : Page
{
    private bool _loading = true;

    public SettingsPage()
    {
        InitializeComponent();
        RestorePointToggle.IsOn = App.Settings.CreateRestorePointBeforeApply;
        ConfirmAdvancedToggle.IsOn = App.Settings.ConfirmAdvanced;
        ThemeCombo.SelectedIndex = App.Settings.Theme switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0,
        };
        _loading = false;
    }

    private void RestorePointToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.CreateRestorePointBeforeApply = RestorePointToggle.IsOn;
        App.Settings.Save();
    }

    private void ConfirmAdvancedToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.ConfirmAdvanced = ConfirmAdvancedToggle.IsOn;
        App.Settings.Save();
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        App.Settings.Theme = ThemeCombo.SelectedIndex switch
        {
            1 => "Light",
            2 => "Dark",
            _ => "System",
        };
        App.Settings.Save();
        App.Window.ApplyTheme(App.Settings.Theme);
    }

    private void Redetect_Click(object sender, RoutedEventArgs e)
    {
        App.Log.Write("Re-detecting all tweak states...");
        System.Threading.Tasks.Task.Run(() =>
        {
            Detector.DetectAll(App.Catalog, App.Store);
            DispatcherQueue.TryEnqueue(() =>
                App.Window.ShowInfo("State detection finished", "Cards now show live detected states where available.", InfoBarSeverity.Success));
        });
    }

    private void OpenData_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo { FileName = CatalogService.DataDir, UseShellExecute = true });

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.GetDirectoryName(App.Log.LogFilePath);
        if (dir != null)
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
    }
}
