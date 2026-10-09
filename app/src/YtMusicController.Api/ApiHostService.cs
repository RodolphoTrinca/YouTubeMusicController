using System.IO;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YtMusicController.Core.Configuration;
using YtMusicController.Core.Player;

namespace YtMusicController.Api;

public sealed class ApiHostService(
    IYouTubeMusicController controller,
    IAppSettingsStore settings,
    ILoggerFactory loggerFactory,
    ILogger<ApiHostService> logger) : IHostedService, IApiStatus
{
    private WebApplication? _application;

    internal Func<WebApplication, CancellationToken, Task> StartApplicationAsync { get; init; } =
        (application, cancellationToken) => application.StartAsync(cancellationToken);

    public bool IsRunning { get; private set; }
    public int Port { get; private set; }
    public string? BaseUrl => IsRunning ? $"http://127.0.0.1:{Port}" : null;
    public bool UsedFallbackPort { get; private set; }
    public string? ErrorMessage { get; private set; }
    public event EventHandler? StatusChanged;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        UsedFallbackPort = false;
        ErrorMessage = null;
        var preferredPort = settings.Current.Port;
        try
        {
            await StartServerAsync(preferredPort, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
            logger.LogWarning(ex,
                "Preferred local API port {Port} is unavailable; requesting an available loopback port",
                preferredPort);
            await DisposeFailedApplicationAsync();

            try
            {
                await StartServerAsync(0, cancellationToken);
            }
            catch (Exception fallbackException) when (fallbackException is IOException or SocketException)
            {
                await DisposeFailedApplicationAsync();
                IsRunning = false;
                ErrorMessage = "Companion controls are unavailable because Windows could not open a local connection. You can continue using the music player.";
                logger.LogError(fallbackException, "Local API could not bind to an available loopback port");
                return;
            }

            UsedFallbackPort = true;
            try
            {
                var current = settings.Current;
                settings.Update(
                    Port,
                    current.VolumeStep,
                    current.VolumeControlMethod,
                    current.MinimizeToTray,
                    current.ShowDebugLogWindow);
            }
            catch (Exception persistenceException) when (persistenceException is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(persistenceException,
                    "The selected API port {Port} could not be persisted; the API remains available for this run",
                    Port);
            }

            logger.LogWarning(
                "Local API selected {BaseUrl}; use this URL as Companion's Generic HTTP base URL",
                BaseUrl);
        }
        finally
        {
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task StartServerAsync(int requestedPort, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new ForwardingLoggerProvider(loggerFactory));
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, requestedPort, listen => listen.Protocols = HttpProtocols.Http1));
        builder.Services.AddSingleton(controller);
        builder.Services.AddSingleton(settings);

        _application = ApiApplication.Configure(builder.Build());
        await StartApplicationAsync(_application, cancellationToken);

        Port = GetBoundPort(_application, requestedPort);
        IsRunning = true;
        ErrorMessage = null;
        logger.LogInformation("Local API listening on {BaseUrl}", BaseUrl);
    }

    private static int GetBoundPort(WebApplication application, int requestedPort)
    {
        var addresses = application.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()?
            .Addresses;

        if (addresses is not null)
        {
            foreach (var address in addresses)
            {
                if (Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Port > 0)
                    return uri.Port;
            }
        }

        if (requestedPort > 0)
            return requestedPort;

        throw new IOException("Kestrel did not report its dynamically selected port.");
    }

    private async Task DisposeFailedApplicationAsync()
    {
        if (_application is null)
            return;
        await _application.DisposeAsync();
        _application = null;
        IsRunning = false;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_application is null)
            return;

        await _application.StopAsync(cancellationToken);
        await _application.DisposeAsync();
        _application = null;
        IsRunning = false;
        StatusChanged?.Invoke(this, EventArgs.Empty);
        logger.LogInformation("Local API stopped");
    }

    private sealed class ForwardingLoggerProvider(ILoggerFactory factory) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => factory.CreateLogger(categoryName);
        public void Dispose() { }
    }
}
