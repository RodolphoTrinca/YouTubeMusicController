using System.Windows;
using System.Windows.Controls;
using YtMusicController.Api;
using YtMusicController.App.Diagnostics;
using YtMusicController.Core.Configuration;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;

namespace YtMusicController.App;

public partial class SettingsWindow : Window
{
    private readonly IAppSettingsStore _settings;
    private readonly DebugLogWindowManager _debugLogWindowManager;

    public SettingsWindow(
        IAppSettingsStore settings,
        IApiStatus apiStatus,
        DebugLogWindowManager debugLogWindowManager)
    {
        InitializeComponent();
        _settings = settings;
        _debugLogWindowManager = debugLogWindowManager;
        var current = settings.Current;
        ActiveApiUrlBox.Text = apiStatus.BaseUrl ?? apiStatus.ErrorMessage ?? "API unavailable";
        PortBox.Text = current.Port.ToString();
        MinimizeToTrayBox.IsChecked = current.MinimizeToTray;
        ShowDebugLogWindowBox.IsChecked = current.ShowDebugLogWindow;
        TokenBox.Text = settings.GetApiToken();
        foreach (ComboBoxItem item in VolumeStepBox.Items)
        {
            if (item.Content?.ToString() == current.VolumeStep.ToString())
            {
                VolumeStepBox.SelectedItem = item;
                break;
            }
        }
        foreach (ComboBoxItem item in VolumeControlMethodBox.Items)
        {
            if (item.Tag?.ToString() == current.VolumeControlMethod.ToString())
            {
                VolumeControlMethodBox.SelectedItem = item;
                break;
            }
        }
    }

    private void DebugWindowSetting_Changed(object sender, RoutedEventArgs eventArgs)
    {
        if (OpenDebugLogButton is not null)
            OpenDebugLogButton.IsEnabled = ShowDebugLogWindowBox.IsChecked == true;
    }

    private void OpenDebugLog_Click(object sender, RoutedEventArgs eventArgs) =>
        _debugLogWindowManager.Show();

    private void CopyApiUrl_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (!Uri.TryCreate(ActiveApiUrlBox.Text, UriKind.Absolute, out _))
            return;
        Clipboard.SetText(ActiveApiUrlBox.Text);
        MessageBox.Show(this,
            "API URL copied. Use it as Companion's Generic HTTP base URL.",
            "YtMusicController", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyToken_Click(object sender, RoutedEventArgs eventArgs)
    {
        Clipboard.SetText(TokenBox.Text);
        MessageBox.Show(this, "Token copied to the clipboard.", "YtMusicController",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RegenerateToken_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (MessageBox.Show(this,
                "Regenerating the token immediately invalidates the token configured in Companion. Continue?",
                "Regenerate token", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        TokenBox.Text = _settings.RegenerateApiToken();
    }

    private void Save_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (!int.TryParse(PortBox.Text, out var port) ||
            VolumeStepBox.SelectedItem is not ComboBoxItem item ||
            !int.TryParse(item.Content?.ToString(), out var step) ||
            VolumeControlMethodBox.SelectedItem is not ComboBoxItem methodItem ||
            !Enum.TryParse<VolumeControlMethod>(
                methodItem.Tag?.ToString(), out var volumeControlMethod))
        {
            MessageBox.Show(this,
                "Enter a valid port, volume step, and volume control method.", "Invalid settings",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var oldPort = _settings.Current.Port;
            var showDebugLogWindow = ShowDebugLogWindowBox.IsChecked == true;
            _settings.Update(
                port,
                step,
                volumeControlMethod,
                MinimizeToTrayBox.IsChecked == true,
                showDebugLogWindow);
            _debugLogWindowManager.SetEnabled(showDebugLogWindow);
            if (oldPort != port)
                MessageBox.Show(this, "Restart the application to apply the preferred API port.",
                    "Restart required", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            MessageBox.Show(this, ex.Message, "Invalid settings",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
