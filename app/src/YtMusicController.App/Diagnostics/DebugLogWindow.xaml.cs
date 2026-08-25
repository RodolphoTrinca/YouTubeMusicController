using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Clipboard = System.Windows.Clipboard;

namespace YtMusicController.App.Diagnostics;

public partial class DebugLogWindow : Window
{
    private readonly DebugLogStore _store;
    private int _displayedEntries;

    public DebugLogWindow(DebugLogStore store)
    {
        InitializeComponent();
        _store = store;
        _store.EntryAdded += OnEntryAdded;
        _store.Cleared += OnCleared;
        Loaded += (_, _) => RefreshFromStore();
        Closed += (_, _) =>
        {
            _store.EntryAdded -= OnEntryAdded;
            _store.Cleared -= OnCleared;
        };
        UpdateStatus();
    }

    private void OnEntryAdded(object? sender, string entry) =>
        Dispatcher.BeginInvoke(() => AppendEntry(entry), DispatcherPriority.Background);

    private void OnCleared(object? sender, EventArgs eventArgs) =>
        Dispatcher.BeginInvoke(() =>
        {
            LogBox.Clear();
            _displayedEntries = 0;
            UpdateStatus();
        });

    private void AppendEntry(string entry)
    {
        if (_displayedEntries >= DebugLogStore.Capacity)
        {
            RefreshFromStore();
            return;
        }

        if (LogBox.Text.Length > 0)
            LogBox.AppendText(Environment.NewLine);
        LogBox.AppendText(entry);
        _displayedEntries++;
        if (AutoScrollBox.IsChecked == true)
            LogBox.ScrollToEnd();
        UpdateStatus();
    }

    private void RefreshFromStore()
    {
        LogBox.Text = _store.GetSnapshotText();
        _displayedEntries = _store.Count;
        if (AutoScrollBox.IsChecked == true)
            LogBox.ScrollToEnd();
        UpdateStatus();
    }

    private void CopyAll_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (!string.IsNullOrEmpty(LogBox.Text))
            Clipboard.SetText(LogBox.Text);
    }

    private void Clear_Click(object sender, RoutedEventArgs eventArgs) => _store.Clear();

    private void OpenLogFolder_Click(object sender, RoutedEventArgs eventArgs)
    {
        Directory.CreateDirectory(DebugLogStore.LogDirectory);
        Process.Start(new ProcessStartInfo(DebugLogStore.LogDirectory) { UseShellExecute = true });
    }

    private void UpdateStatus() =>
        StatusText.Text = $"Verbose DEBUG logging enabled · Showing the latest {Math.Min(_displayedEntries, DebugLogStore.Capacity):N0} entries · Log files: {DebugLogStore.LogDirectory}";
}
