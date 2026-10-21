using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using UltimateWinUI.Models;

namespace UltimateWinUI.Views;

/// <summary>
/// Renders a whole category (or search results) from the catalog. Every card
/// is built in code from the tweak's control type:
///
///   toggle   -> ToggleSwitch between the two options
///   dropdown -> ComboBox of options + Apply button
///   button   -> a single Run button
///   actions  -> one button per option (open / run / check style scripts)
///
/// A Check button is added whenever the upstream script ships a check option.
/// </summary>
public sealed partial class CategoryPage : Page
{
    private Category? _category;
    private string? _searchQuery;
    private bool _building;

    public CategoryPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        switch (e.Parameter)
        {
            case Category cat:
                _category = cat;
                _searchQuery = null;
                break;
            case string s when s.StartsWith("search:", StringComparison.Ordinal):
                _category = null;
                _searchQuery = s["search:".Length..];
                break;
        }
        RefreshCards();
    }

    public void RefreshCards()
    {
        if (_building) return;
        _building = true;
        try
        {
            RootPanel.Children.Clear();

            var title = _searchQuery != null ? $"Results for \"{_searchQuery}\"" : _category?.Name ?? "";
            RootPanel.Children.Add(new TextBlock
            {
                Text = title,
                Style = (Style)Application.Current.Resources["PageTitleStyle"],
            });
            if (_category != null)
            {
                RootPanel.Children.Add(new TextBlock
                {
                    Text = _category.Description,
                    Style = (Style)Application.Current.Resources["CaptionStyle"],
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 0, 12),
                });
            }

            var tweaks = GetTweaks();
            if (tweaks.Count == 0)
            {
                RootPanel.Children.Add(new TextBlock
                {
                    Text = "No tweaks match.",
                    Style = (Style)Application.Current.Resources["CaptionStyle"],
                    Margin = new Thickness(0, 12, 0, 0),
                });
                return;
            }
            foreach (var tweak in tweaks)
                RootPanel.Children.Add(BuildCard(tweak));
        }
        finally
        {
            _building = false;
        }
    }

    private List<Tweak> GetTweaks()
    {
        var list = new List<Tweak>();
        if (_searchQuery == null)
        {
            if (_category != null) list.AddRange(_category.Tweaks);
            return list;
        }
        var q = _searchQuery.ToLowerInvariant();
        foreach (var cat in App.Catalog.Categories)
            foreach (var tweak in cat.Tweaks)
                if (tweak.Name.ToLowerInvariant().Contains(q) ||
                    tweak.Script.ToLowerInvariant().Contains(q))
                    list.Add(tweak);
        return list;
    }

    // ------------------------------------------------------------ card layout

    private Border BuildCard(Tweak tweak)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new FontIcon
        {
            Glyph = tweak.Parent?.IconGlyph ?? "\uE8A5",
            FontSize = 20,
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        textStack.Children.Add(new TextBlock
        {
            Text = tweak.Name,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        if (!string.IsNullOrWhiteSpace(tweak.Description))
        {
            textStack.Children.Add(new TextBlock
            {
                Text = tweak.Description,
                Style = (Style)Application.Current.Resources["CaptionStyle"],
                FontSize = 12.5,
            });
        }
        var status = tweak.StatusText;
        if (_searchQuery != null && tweak.Parent != null)
            status = tweak.Parent.Name + " · " + status;
        status += "  ·  " + tweak.Script;
        var statusBlock = new TextBlock
        {
            Text = status,
            Style = (Style)Application.Current.Resources["CaptionStyle"],
        };
        textStack.Children.Add(statusBlock);
        if (tweak.Risk == "high")
        {
            var warn = new TextBlock
            {
                Text = "High risk: can disable security features or change low level system behavior.",
                FontSize = 12,
            };
            if (Application.Current.Resources.TryGetValue("SystemFillColorCriticalBrush", out var brush) && brush is Brush b)
                warn.Foreground = b;
            textStack.Children.Add(warn);
        }
        Grid.SetColumn(textStack, 1);
        grid.Children.Add(textStack);

        var controlHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        BuildControls(tweak, controlHost);
        if (tweak.IsBusy)
        {
            controlHost.Children.Add(new ProgressRing { IsActive = true, Width = 22, Height = 22 });
        }
        Grid.SetColumn(controlHost, 2);
        grid.Children.Add(controlHost);

        return new Border
        {
            Style = (Style)Application.Current.Resources["TweakCardStyle"],
            Child = grid,
            Opacity = tweak.IsBusy ? 0.6 : 1.0,
        };
    }

    // ------------------------------------------------------------ controls

    private void BuildControls(Tweak tweak, StackPanel host)
    {
        var enabled = !tweak.IsBusy;
        switch (tweak.Control)
        {
            case "toggle" when tweak.ChoiceOptions.Count == 2:
            {
                var active = tweak.ActiveOption!;
                var other = tweak.ChoiceOptions[0] == active ? tweak.ChoiceOptions[1] : tweak.ChoiceOptions[0];
                var sw = new ToggleSwitch
                {
                    IsEnabled = enabled,
                    OnContent = "",
                    OffContent = "",
                    Tag = tweak,
                };
                sw.IsOn = tweak.CurrentOptionLabel == active.Label;
                sw.Toggled += async (_, _) =>
                {
                    if (_building) return;
                    var target = sw.IsOn ? active : other;
                    if (tweak.CurrentOptionLabel != target.Label)
                        await RunWithInputsAsync(tweak, target);
                    else
                        RefreshCards();
                };
                host.Children.Add(sw);
                break;
            }
            case "dropdown" when tweak.ChoiceOptions.Count > 0:
            {
                var combo = new ComboBox { IsEnabled = enabled, MinWidth = 200 };
                foreach (var o in tweak.ChoiceOptions) combo.Items.Add(o.Label);
                combo.SelectedItem = tweak.CurrentOptionLabel;
                var apply = new Button { Content = "Apply", IsEnabled = enabled };
                apply.Click += async (_, _) =>
                {
                    if (combo.SelectedItem is string label)
                    {
                        var option = tweak.ChoiceOptions.Find(o => o.Label == label);
                        if (option != null) await RunWithInputsAsync(tweak, option);
                    }
                };
                host.Children.Add(combo);
                host.Children.Add(apply);
                break;
            }
            case "button" when tweak.ChoiceOptions.Count > 0:
            {
                var option = tweak.ChoiceOptions[0];
                var btn = new Button { Content = ShortLabel(tweak, option), IsEnabled = enabled };
                btn.Click += async (_, _) => await RunWithInputsAsync(tweak, option);
                host.Children.Add(btn);
                break;
            }
            default: // actions: one button per option
            {
                foreach (var option in tweak.Options)
                {
                    var btn = new Button { Content = ShortLabel(tweak, option), IsEnabled = enabled };
                    var captured = option;
                    btn.Click += async (_, _) => await RunWithInputsAsync(tweak, captured);
                    host.Children.Add(btn);
                }
                break;
            }
        }

        // Scripts with a separate Check option get a Check button next to any control type.
        if (tweak.Control != "actions" && tweak.CheckOption is { } check)
        {
            var btn = new Button { Content = "Check", IsEnabled = enabled };
            btn.Click += async (_, _) =>
            {
                await RunWithInputsAsync(tweak, check);
                App.Window.NavigateTo("logs");
            };
            host.Children.Add(btn);
        }
    }

    /// <summary>
    /// Some scripts ask for values (account name, USB drive letter, a game exe
    /// id). Collect them in a dialog and feed them to the script over stdin.
    /// </summary>
    private async Task RunWithInputsAsync(Tweak tweak, TweakOption option)
    {
        if (option.Inputs is not { Count: > 0 } prompts)
        {
            if (!await App.ApplyOptionAsync(tweak, option))
                RefreshCards();
            return;
        }

        var panel = new StackPanel { Spacing = 10, MinWidth = 320 };
        var boxes = new List<TextBox>();
        foreach (var prompt in prompts)
        {
            panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap });
            var box = new TextBox();
            boxes.Add(box);
            panel.Children.Add(box);
        }
        var dialog = new ContentDialog
        {
            Title = tweak.Name,
            Content = panel,
            PrimaryButtonText = "Run",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            RefreshCards();
            return;
        }

        var answers = new List<string>();
        foreach (var box in boxes) answers.Add(box.Text.Trim());
        if (!await App.ApplyOptionAsync(tweak, option, answers))
            RefreshCards();
    }

    /// <summary>"Power Plan: On (Recommended)" -> "On (Recommended)" when it just repeats the tweak name.</summary>
    private static string ShortLabel(Tweak tweak, TweakOption option)
    {
        var label = option.Label;
        var prefix = tweak.Name + ":";
        if (label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            label = label[prefix.Length..].Trim();
        if (option.Kind == "check" && !label.Contains("Check", StringComparison.OrdinalIgnoreCase))
            label = "Check";
        return label.Length == 0 ? option.Kind : label;
    }
}
