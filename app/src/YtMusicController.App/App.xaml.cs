using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using YtMusicController.Api;
using YtMusicController.App.Diagnostics;
using YtMusicController.App.Tray;
using YtMusicController.Core.Configuration;
using YtMusicController.Core.Player;
using YtMusicController.WebView;
using MessageBox = System.Windows.MessageBox;

namespace YtMusicController.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private TrayIconService? _tray;
    private DebugLogWindowManager? _debugLogWindows;
    private SingleInstanceCoordinator? _singleInstance;
    private bool _pendingActivation;

    protected override async void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);
        _singleInstance = new SingleInstanceCoordinator();
        if (!_singleInstance.IsPrimary)
        {
            await _singleInstance.SignalPrimaryAsync();
            Shutdown();
            return;
        }
        _singleInstance.Listen(() => Dispatcher.BeginInvoke(() =>
        {
            if (MainWindow is MainWindow window)
                window.ShowFromTray();
            else
                _pendingActivation = true;
        }));
        ConfigureSerilog(out var logStore);

        try
        {
            var builder = Host.CreateApplicationBuilder(eventArgs.Args);
            builder.Logging.ClearProviders();
            builder.Services.AddSerilog(Log.Logger, dispose: false);
            builder.Services.AddSingleton(logStore);
            builder.Services.AddSingleton<DebugLogWindowManager>();
            builder.Services.AddSingleton<IAppSettingsStore, AppSettingsStore>();
            builder.Services.AddSingleton<WebViewYouTubeMusicController>();
            builder.Services.AddSingleton<IYouTubeMusicController>(services =>
                services.GetRequiredService<WebViewYouTubeMusicController>());
            builder.Services.AddSingleton<ApiHostService>();
            builder.Services.AddSingleton<IApiStatus>(services => services.GetRequiredService<ApiHostService>());
            builder.Services.AddHostedService(services => services.GetRequiredService<ApiHostService>());
            builder.Services.AddSingleton<MainWindow>();
            builder.Services.AddTransient<SettingsWindow>();
            builder.Services.AddSingleton<TrayIconService>();

            _host = builder.Build();
            var settings = _host.Services.GetRequiredService<IAppSettingsStore>();
            _debugLogWindows = _host.Services.GetRequiredService<DebugLogWindowManager>();
            if (settings.Current.ShowDebugLogWindow)
                _debugLogWindows.Show();

            Log.Information("YtMusicController starting");
            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.SettingsRequested += (_, _) => ShowSettings(mainWindow);
            mainWindow.Show();
            if (_pendingActivation)
                mainWindow.ShowFromTray();

            _tray = _host.Services.GetRequiredService<TrayIconService>();
            _tray.Initialize(
                mainWindow.ShowFromTray,
                () => ShowSettings(mainWindow),
                () =>
                {
                    mainWindow.AllowClose();
                    Shutdown();
                });
            Log.Information("YtMusicController started");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "YtMusicController startup failed");
            var keepDebugWindowOpen = TryShowDebugWindowAfterFailure();
            MessageBox.Show(
                $"YtMusicController could not start.\n\n{ex.Message}\n\n" +
                (keepDebugWindowOpen
                    ? "Review the Debug Log window for the complete error."
                    : $"Logs: {DebugLogStore.LogDirectory}"),
                "Startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            if (!keepDebugWindowOpen)
                Shutdown(1);
        }
    }

    private static void ConfigureSerilog(out DebugLogStore logStore)
    {
        logStore = new DebugLogStore();
        Directory.CreateDirectory(DebugLogStore.LogDirectory);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Debug)
            .MinimumLevel.Override("Microsoft.Extensions.Hosting", LogEventLevel.Debug)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Sink(logStore)
            .WriteTo.File(
                DebugLogStore.RollingLogPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1),
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] " +
                                "{SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        Log.Debug("Verbose diagnostic logging enabled; panel capacity {Capacity}; log directory {LogDirectory}",
            DebugLogStore.Capacity, DebugLogStore.LogDirectory);
    }

    private bool TryShowDebugWindowAfterFailure()
    {
        try
        {
            if (_host?.Services.GetService<IAppSettingsStore>()?.Current.ShowDebugLogWindow != true)
                return false;
            _debugLogWindows ??= _host.Services.GetRequiredService<DebugLogWindowManager>();
            _debugLogWindows.Show();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "The debug log window could not be shown after startup failure");
            return false;
        }
    }

    private void ShowSettings(Window owner)
    {
        if (_host is null)
            return;
        var settingsWindow = _host.Services.GetRequiredService<SettingsWindow>();
        settingsWindow.Owner = owner;
        settingsWindow.ShowDialog();
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        try
        {
            _tray?.Dispose();
            _debugLogWindows?.Close();
            if (_host is not null)
            {
                try
                {
                    // Finish releasing the API port before another instance can start.
                    Task.Run(() => _host.StopAsync(TimeSpan.FromSeconds(5)))
                        .GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Application host shutdown failed");
                }
                finally
                {
                    _host.Dispose();
                }
            }

            Log.Information("YtMusicController stopped");
        }
        finally
        {
            _singleInstance?.Dispose();
            Log.CloseAndFlush();
            base.OnExit(eventArgs);
        }
    }
}
