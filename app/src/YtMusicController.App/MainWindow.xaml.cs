using System.ComponentModel;
using System.Windows;
using Brushes = System.Windows.Media.Brushes;
using Microsoft.Extensions.Logging;
using YtMusicController.Api;
using YtMusicController.Core.Configuration;
using YtMusicController.Core.Player;
using YtMusicController.WebView;

namespace YtMusicController.App;

public partial class MainWindow : Window
{
    private readonly WebViewYouTubeMusicController _controller;
    private readonly IAppSettingsStore _settings;
    private readonly IApiStatus _apiStatus;
    private readonly ILogger<MainWindow> _logger;
    private bool _allowClose;

    public MainWindow(
        WebViewYouTubeMusicController controller,
        IAppSettingsStore settings,
        IApiStatus apiStatus,
        ILogger<MainWindow> logger)
    {
        InitializeComponent();
        _controller = controller;
        _settings = settings;
        _apiStatus = apiStatus;
        _logger = logger;
        Loaded += OnLoaded;
        Closing += OnClosing;
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized && _settings.Current.MinimizeToTray)
                Hide();
        };
        _controller.StateChanged += OnPlayerStateChanged;
        _apiStatus.StatusChanged += OnApiStatusChanged;
        UpdateApiStatus();
    }

    public event EventHandler? SettingsRequested;

    private async void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        Loaded -= OnLoaded;
        try
        {
            await _controller.InitializeAsync(MusicWebView);
            UpdateApiStatus();
            PlayerText.Text = "Waiting for the YouTube Music player";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebView2 initialization failed");
            ConnectionText.Text = "WebView2 failed to initialize";
            ConnectionText.Foreground = Brushes.OrangeRed;
            PlayerText.Text = ex.Message;
        }
    }

    private void OnApiStatusChanged(object? sender, EventArgs e) =>
        Dispatcher.InvokeAsync(UpdateApiStatus);

    private void UpdateApiStatus()
    {
        if (_apiStatus.IsRunning)
        {
            ConnectionText.Text = _apiStatus.UsedFallbackPort
                ? "API: " + _apiStatus.BaseUrl + " (selected automatically - use this in Companion)"
                : "API: " + _apiStatus.BaseUrl;
            ConnectionText.Foreground = Brushes.LightGreen;
        }
        else
        {
            ConnectionText.Text = _apiStatus.ErrorMessage ?? "Local API is starting…";
            ConnectionText.Foreground = Brushes.OrangeRed;
        }
    }

    private void OnPlayerStateChanged(object? sender, PlayerState state)
    {
        Dispatcher.InvokeAsync(() =>
        {
            UpdateApiStatus();
            PlayerText.Text = state.IsPlaying ? "Playing" : "Paused";
            PlayerText.Text += $" · Volume {state.Volume}%{(state.IsMuted ? " · Muted" : string.Empty)}";
            TrackText.Text = state.Track is null
                ? "No track metadata"
                : string.Join(" — ", new[] { state.Track.Title, state.Track.Artist }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
        });
    }

    private void Settings_Click(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose && _settings.Current.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
        }
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public void AllowClose()
    {
        _allowClose = true;
        Close();
    }
}
