using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using YtMusicController.Api;
using YtMusicController.Core.Configuration;
using YtMusicController.Core.Player;

namespace YtMusicController.Tests;

public sealed class ApiHostServiceTests
{
    [Fact]
    public async Task Busy_preferred_port_selects_persists_and_reports_available_port()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var occupiedPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        var settings = new FakeSettings(occupiedPort);
        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None));
        var service = new ApiHostService(
            new FakeController(),
            settings,
            loggerFactory,
            loggerFactory.CreateLogger<ApiHostService>());

        try
        {
            var exception = await Record.ExceptionAsync(() => service.StartAsync(CancellationToken.None));

            Assert.Null(exception);
            Assert.True(service.IsRunning);
            Assert.True(service.UsedFallbackPort);
            Assert.NotEqual(occupiedPort, service.Port);
            Assert.Equal(service.Port, settings.Current.Port);
            Assert.Equal($"http://127.0.0.1:{service.Port}", service.BaseUrl);

            using var client = new HttpClient { BaseAddress = new Uri(service.BaseUrl!) };
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", settings.GetApiToken());
            var response = await client.GetAsync("/api/player/status");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Socket_access_denied_selects_available_port_and_serves_requests()
    {
        var settings = new FakeSettings(54088);
        using var logs = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None));
        var attempts = 0;
        var notifications = 0;
        var service = new ApiHostService(new FakeController(), settings, logs, logs.CreateLogger<ApiHostService>())
        {
            StartApplicationAsync = (application, token) =>
                ++attempts == 1
                    ? Task.FromException(new SocketException(10013))
                    : application.StartAsync(token)
        };
        service.StatusChanged += (_, _) => notifications++;

        try
        {
            await service.StartAsync(CancellationToken.None);
            Assert.Equal(2, attempts);
            Assert.Equal(1, notifications);
            Assert.True(service.IsRunning);
            Assert.True(service.UsedFallbackPort);
            Assert.Null(service.ErrorMessage);
            Assert.InRange(service.Port, 1, 65535);
            Assert.Equal(service.Port, settings.Current.Port);
            using var client = new HttpClient { BaseAddress = new Uri(service.BaseUrl!) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.GetApiToken());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/player/status")).StatusCode);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Socket_failure_on_both_attempts_does_not_abort_application_startup()
    {
        var settings = new FakeSettings(54088);
        using var logs = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None));
        var attempts = 0;
        var notifications = 0;
        var service = new ApiHostService(new FakeController(), settings, logs, logs.CreateLogger<ApiHostService>())
        {
            StartApplicationAsync = (_, _) =>
            {
                attempts++;
                return Task.FromException(new SocketException(10013));
            }
        };
        service.StatusChanged += (_, _) => notifications++;

        await service.StartAsync(CancellationToken.None);

        Assert.Equal(2, attempts);
        Assert.Equal(1, notifications);
        Assert.False(service.IsRunning);
        Assert.False(service.UsedFallbackPort);
        Assert.Null(service.BaseUrl);
        Assert.NotNull(service.ErrorMessage);
        Assert.Equal(54088, settings.Current.Port);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Cancellation_is_not_treated_as_a_port_failure()
    {
        var settings = new FakeSettings(54088);
        using var logs = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None));
        var attempts = 0;
        var service = new ApiHostService(new FakeController(), settings, logs, logs.CreateLogger<ApiHostService>())
        {
            StartApplicationAsync = (_, _) =>
            {
                attempts++;
                return Task.FromCanceled(new CancellationToken(true));
            }
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.StartAsync(CancellationToken.None));
        Assert.Equal(1, attempts);
        Assert.False(service.UsedFallbackPort);
        await service.StopAsync(CancellationToken.None);
    }

    private sealed class FakeSettings(int port) : IAppSettingsStore
    {
        public AppSettings Current { get; private set; } = new() { Port = port };
        public string GetApiToken() => "test-token";
        public AppSettings Update(
            int newPort,
            int volumeStep,
            VolumeControlMethod volumeControlMethod,
            bool minimizeToTray,
            bool showDebugLogWindow) =>
            Current = Current with
            {
                Port = newPort,
                VolumeStep = volumeStep,
                VolumeControlMethod = volumeControlMethod,
                MinimizeToTray = minimizeToTray,
                ShowDebugLogWindow = showDebugLogWindow
            };
        public string RegenerateApiToken() => "test-token";
    }

    private sealed class FakeController : IYouTubeMusicController
    {
        public event EventHandler<PlayerState>? StateChanged { add { } remove { } }

        public Task PlayAsync() => Task.CompletedTask;
        public Task PauseAsync() => Task.CompletedTask;
        public Task TogglePlayPauseAsync() => Task.CompletedTask;
        public Task NextAsync() => Task.CompletedTask;
        public Task PreviousAsync() => Task.CompletedTask;
        public Task SetVolumeAsync(int percentage) => Task.CompletedTask;
        public Task IncreaseVolumeAsync(int step) => Task.CompletedTask;
        public Task DecreaseVolumeAsync(int step) => Task.CompletedTask;
        public Task MuteAsync() => Task.CompletedTask;
        public Task UnmuteAsync() => Task.CompletedTask;
        public Task ToggleMuteAsync() => Task.CompletedTask;
        public Task LikeAsync() => Task.CompletedTask;
        public Task UnlikeAsync() => Task.CompletedTask;
        public Task ToggleLikeAsync() => Task.CompletedTask;
        public Task<PlayerState> GetStateAsync() => Task.FromResult(PlayerState.Empty);
    }
}
