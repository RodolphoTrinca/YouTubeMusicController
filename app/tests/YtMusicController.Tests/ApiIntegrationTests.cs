using System.Net;
using System.Net.WebSockets;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using YtMusicController.Api;
using YtMusicController.Core.Configuration;
using YtMusicController.Core.Player;

namespace YtMusicController.Tests;

public sealed class ApiIntegrationTests
{
    [Fact]
    public async Task Volume_up_uses_configured_step()
    {
        await using var fixture = await ApiFixture.CreateAsync(step: 5);
        var response = await fixture.Client.PostAsync("/api/volume/up", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5, fixture.Controller.LastIncrease);
    }

    [Fact]
    public async Task Invalid_or_missing_token_is_rejected()
    {
        await using var fixture = await ApiFixture.CreateAsync();
        fixture.Client.DefaultRequestHeaders.Authorization = null;
        var response = await fixture.Client.PostAsync("/api/player/toggle", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, fixture.Controller.ToggleCalls);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Invalid_volume_is_bad_request(int volume)
    {
        await using var fixture = await ApiFixture.CreateAsync();
        var response = await fixture.Client.PutAsync($"/api/volume/{volume}", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(fixture.Controller.LastSetVolume);
    }

    [Fact]
    public async Task Status_serializes_player_state()
    {
        await using var fixture = await ApiFixture.CreateAsync();
        fixture.Controller.State = new PlayerState(true, false, 65, true,
            new TrackInfo("Everlong", "Foo Fighters", null, null));

        var response = await fixture.Client.GetAsync("/api/player/status");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("isPlaying").GetBoolean());
        Assert.Equal(65, json.RootElement.GetProperty("volume").GetInt32());
        Assert.Equal("Everlong", json.RootElement.GetProperty("track").GetProperty("title").GetString());
    }

    [Fact]
    public async Task Toggle_like_route_invokes_controller()
    {
        await using var fixture = await ApiFixture.CreateAsync();
        var response = await fixture.Client.PostAsync("/api/player/toggle-like", null);
        response.EnsureSuccessStatusCode();
        Assert.Equal(1, fixture.Controller.ToggleLikeCalls);
    }

    [Fact]
    public async Task WebSocket_sends_initial_and_changed_player_state()
    {
        await using var fixture = await ApiFixture.CreateAsync();
        fixture.Controller.State = new PlayerState(false, false, 50, false,
            new TrackInfo("Initial", "Artist", null, null));
        using var socket = await fixture.ConnectEventsAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var buffer = new byte[4096];

        var initialResult = await socket.ReceiveAsync(buffer, timeout.Token);
        using (var initial = JsonDocument.Parse(buffer.AsMemory(0, initialResult.Count)))
        {
            Assert.False(initial.RootElement.GetProperty("isLiked").GetBoolean());
            Assert.Equal("Initial", initial.RootElement.GetProperty("track").GetProperty("title").GetString());
        }

        fixture.Controller.Publish(new PlayerState(true, false, 50, true,
            new TrackInfo("Changed", "Artist", null, null)));
        var changedResult = await socket.ReceiveAsync(buffer, timeout.Token);
        using var changed = JsonDocument.Parse(buffer.AsMemory(0, changedResult.Count));
        Assert.True(changed.RootElement.GetProperty("isLiked").GetBoolean());
        Assert.Equal("Changed", changed.RootElement.GetProperty("track").GetProperty("title").GetString());
    }

    private sealed class ApiFixture : IAsyncDisposable
    {
        private const string Token = "test-token-that-is-not-a-secret";
        private readonly WebApplication _application;

        private ApiFixture(WebApplication application, HttpClient client, FakeController controller)
        {
            _application = application;
            Client = client;
            Controller = controller;
        }

        public HttpClient Client { get; }
        public FakeController Controller { get; }

        public Task<WebSocket> ConnectEventsAsync()
        {
            var client = _application.GetTestServer().CreateWebSocketClient();
            var encodedToken = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Token))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            client.SubProtocols.Add("ytmusic-controller.auth." + encodedToken);
            return client.ConnectAsync(
                new Uri("ws://localhost/api/player/events"),
                CancellationToken.None);
        }

        public static async Task<ApiFixture> CreateAsync(int step = 5)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var controller = new FakeController();
            builder.Services.AddSingleton<IYouTubeMusicController>(controller);
            builder.Services.AddSingleton<IAppSettingsStore>(new FakeSettings(Token, step));
            var app = ApiApplication.Configure(builder.Build());
            await app.StartAsync();
            var client = app.GetTestClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            return new ApiFixture(app, client, controller);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _application.StopAsync();
            await _application.DisposeAsync();
        }
    }

    private sealed class FakeSettings(string token, int step) : IAppSettingsStore
    {
        public AppSettings Current { get; private set; } = new() { VolumeStep = step };
        public string GetApiToken() => token;
        public AppSettings Update(
            int port,
            int volumeStep,
            VolumeControlMethod volumeControlMethod,
            bool minimizeToTray,
            bool showDebugLogWindow) =>
            Current = Current with
            {
                Port = port,
                VolumeStep = volumeStep,
                VolumeControlMethod = volumeControlMethod,
                MinimizeToTray = minimizeToTray,
                ShowDebugLogWindow = showDebugLogWindow
            };
        public string RegenerateApiToken() => token;
    }

    private sealed class FakeController : IYouTubeMusicController
    {
        public event EventHandler<PlayerState>? StateChanged;

        public int LastIncrease { get; private set; }
        public int? LastSetVolume { get; private set; }
        public int ToggleCalls { get; private set; }
        public int ToggleLikeCalls { get; private set; }
        public PlayerState State { get; set; } = PlayerState.Empty;

        public Task PlayAsync() => Task.CompletedTask;
        public Task PauseAsync() => Task.CompletedTask;
        public Task TogglePlayPauseAsync() { ToggleCalls++; return Task.CompletedTask; }
        public Task NextAsync() => Task.CompletedTask;
        public Task PreviousAsync() => Task.CompletedTask;
        public Task SetVolumeAsync(int percentage) { LastSetVolume = percentage; return Task.CompletedTask; }
        public Task IncreaseVolumeAsync(int step) { LastIncrease = step; return Task.CompletedTask; }
        public Task DecreaseVolumeAsync(int step) => Task.CompletedTask;
        public Task MuteAsync() => Task.CompletedTask;
        public Task UnmuteAsync() => Task.CompletedTask;
        public Task ToggleMuteAsync() => Task.CompletedTask;
        public Task LikeAsync() => Task.CompletedTask;
        public Task UnlikeAsync() => Task.CompletedTask;
        public Task ToggleLikeAsync() { ToggleLikeCalls++; return Task.CompletedTask; }
        public void Publish(PlayerState state)
        {
            State = state;
            StateChanged?.Invoke(this, state);
        }
        public Task<PlayerState> GetStateAsync() => Task.FromResult(State);
    }
}
