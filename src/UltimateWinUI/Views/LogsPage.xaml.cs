using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace UltimateWinUI.Views;

public sealed partial class LogsPage : Page
{
    public LogsPage()
    {
        InitializeComponent();
        LogList.ItemsSource = App.Log.Lines;
        App.Log.Lines.CollectionChanged += Lines_CollectionChanged;
        Unloaded += (_, _) => App.Log.Lines.CollectionChanged -= Lines_CollectionChanged;
        ScrollToEnd();
    }

    private void Lines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            ScrollToEnd();
    }

    private void ScrollToEnd()
    {
        if (App.Log.Lines.Count > 0)
            LogList.ScrollIntoView(App.Log.Lines[^1]);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(string.Join(Environment.NewLine, App.Log.Lines));
        Clipboard.SetContent(package);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.GetDirectoryName(App.Log.LogFilePath);
        if (dir != null)
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => App.Log.Clear();
}
