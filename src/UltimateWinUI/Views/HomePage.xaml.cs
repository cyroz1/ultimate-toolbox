using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using UltimateWinUI.Models;
using UltimateWinUI.Services;

namespace UltimateWinUI.Views;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        CategoryGrid.ItemsSource = App.Catalog.Categories;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        RefreshStats();
    }

    public void RefreshStats()
    {
        var total = 0;
        var detected = 0;
        var applied = 0;
        foreach (var cat in App.Catalog.Categories)
            foreach (var tweak in cat.Tweaks)
            {
                total++;
                if (tweak.CurrentSource == StateSource.Detected) detected++;
                else if (tweak.CurrentSource == StateSource.Applied) applied++;
            }
        TweakCountText.Text = total.ToString();
        DetectedCountText.Text = detected.ToString();
        AppliedCountText.Text = applied.ToString();
    }

    private void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Category cat })
            App.Window.NavigateTo("category:" + cat.Name);
    }

    private void Redetect_Click(object sender, RoutedEventArgs e)
    {
        App.Log.Write("Re-detecting all tweak states...");
        System.Threading.Tasks.Task.Run(() =>
        {
            Detector.DetectAll(App.Catalog, App.Store);
            DispatcherQueue.TryEnqueue(() =>
            {
                RefreshStats();
                App.Window.ShowInfo("State detection finished", "Cards now show live detected states where available.", InfoBarSeverity.Success);
            });
        });
    }

    private void Logs_Click(object sender, RoutedEventArgs e) => App.Window.NavigateTo("logs");
}
