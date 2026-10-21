using System;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UltimateWinUI.Views;

namespace UltimateWinUI;

public sealed partial class MainWindow : Window
{
    private bool _suppressSelection;

    public MainWindow()
    {
        InitializeComponent();

        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var appWindow = GetAppWindow();
        appWindow.Resize(new Windows.Graphics.SizeInt32(1180, 780));
        Title = "UltimateWinUI";

        BuildNavigation();
    }

    private AppWindow GetAppWindow()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var id = Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(id);
    }

    private void BuildNavigation()
    {
        var home = new NavigationViewItem
        {
            Content = "Home",
            Icon = new FontIcon { Glyph = "\uE80F" },
            Tag = "home",
        };
        NavView.MenuItems.Add(home);

        NavView.MenuItems.Add(new NavigationViewItemSeparator());
        foreach (var cat in App.Catalog.Categories)
        {
            NavView.MenuItems.Add(new NavigationViewItem
            {
                Content = cat.Name,
                Icon = new FontIcon { Glyph = cat.IconGlyph },
                Tag = "category:" + cat.Name,
            });
        }

        NavView.FooterMenuItems.Add(new NavigationViewItem
        {
            Content = "Logs",
            Icon = new FontIcon { Glyph = "\uE8A5" },
            Tag = "logs",
        });
        NavView.FooterMenuItems.Add(new NavigationViewItem
        {
            Content = "Settings",
            Icon = new FontIcon { Glyph = "\uE713" },
            Tag = "settings",
        });

        _suppressSelection = true;
        NavView.SelectedItem = home;
        _suppressSelection = false;
    }

    /// <summary>Supports --page home|logs|settings|category:&lt;name&gt; for CI screenshots.</summary>
    public void NavigateStartupPage()
    {
        var target = "home";
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == "--page")
                target = args[i + 1];
        NavigateTo(target);
    }

    public void NavigateTo(string tag)
    {
        switch (tag)
        {
            case "home":
                ContentFrame.Navigate(typeof(HomePage));
                break;
            case "logs":
                ContentFrame.Navigate(typeof(LogsPage));
                break;
            case "settings":
                ContentFrame.Navigate(typeof(SettingsPage));
                break;
            default:
                if (tag.StartsWith("category:", StringComparison.Ordinal))
                {
                    var name = tag["category:".Length..];
                    foreach (var cat in App.Catalog.Categories)
                        if (cat.Name == name)
                        {
                            ContentFrame.Navigate(typeof(CategoryPage), cat);
                            break;
                        }
                }
                else if (tag.StartsWith("search:", StringComparison.Ordinal))
                {
                    ContentFrame.Navigate(typeof(CategoryPage), tag);
                }
                break;
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (_suppressSelection) return;
        if (e.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            NavigateTo(tag);
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.QueryText))
            NavigateTo("search:" + args.QueryText.Trim());
    }

    public void ShowInfo(string title, string message, InfoBarSeverity severity)
    {
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
    }

    /// <summary>Re-runs the current page's navigation so cards re-render with fresh state.</summary>
    public void RefreshCurrentPage()
    {
        if (ContentFrame.Content is CategoryPage page)
            page.RefreshCards();
        else if (ContentFrame.Content is HomePage home)
            home.RefreshStats();
    }

    public void ApplyTheme(string theme)
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
        }
    }
}
