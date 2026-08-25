using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using YtMusicController.Core.Configuration;
using YtMusicController.Core.Player;
using YtMusicController.WebView.Scripts;

namespace YtMusicController.WebView;

public sealed class WebViewYouTubeMusicController(
    ILogger<WebViewYouTubeMusicController> logger,
    IAppSettingsStore settings)
    : IYouTubeMusicController, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly object _stateGate = new();
    private readonly object _volumeGate = new();
    private readonly VolumeTargetAccumulator _volumeTarget = new();
    private volatile WebView2? _webView;
    private volatile Dispatcher? _dispatcher;
    private PlayerState _state = PlayerState.Empty;
    private volatile bool _hasMedia;
    private CancellationTokenSource? _volumeCancellation;
    private volatile bool _disposed;
    private long _commandSequence;

    public event EventHandler<PlayerState>? StateChanged;

    public async Task InitializeAsync(WebView2 webView)
    {
        _webView = webView;
        _dispatcher = webView.Dispatcher;
        Directory.CreateDirectory(WebViewLifecycleService.UserDataFolder);

        var environment = await CoreWebView2Environment.CreateAsync(
            userDataFolder: WebViewLifecycleService.UserDataFolder);
        await webView.EnsureCoreWebView2Async(environment);

        ConfigureSecurity(webView.CoreWebView2);
        webView.NavigationStarting += OnNavigationStarting;
        webView.CoreWebView2.FrameNavigationStarting += OnFrameNavigationStarting;
        webView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
        webView.CoreWebView2.PermissionRequested += (_, eventArgs) => eventArgs.State = CoreWebView2PermissionState.Deny;
        webView.CoreWebView2.DownloadStarting += (_, eventArgs) => eventArgs.Cancel = true;
        webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        webView.CoreWebView2.DOMContentLoaded += (_, eventArgs) =>
            logger.LogDebug("YouTube Music DOM content loaded; navigation {NavigationId}",
                eventArgs.NavigationId);
        webView.CoreWebView2.ProcessFailed += (_, eventArgs) =>
            logger.LogError("WebView2 process failed: {ProcessFailedKind}", eventArgs.ProcessFailedKind);
        webView.NavigationCompleted += (_, eventArgs) =>
        {
            if (eventArgs.IsSuccess)
                logger.LogInformation("YouTube Music navigation completed at {SourceUri}",
                    webView.Source?.AbsoluteUri);
            else
                logger.LogWarning(
                    "YouTube Music navigation failed at {SourceUri}: {Status}",
                    webView.Source?.AbsoluteUri, eventArgs.WebErrorStatus);
        };

        await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(PlayerScripts.StateObserver);
        webView.CoreWebView2.Navigate(WebViewLifecycleService.YouTubeMusicUri.AbsoluteUri);
        logger.LogInformation("WebView2 initialized with isolated profile at {ProfilePath}",
            WebViewLifecycleService.UserDataFolder);
    }

    public Task PlayAsync() => ExecuteCommandAsync("play", PlayerScripts.Play);
    public Task PauseAsync() => ExecuteCommandAsync("pause", PlayerScripts.Pause);
    public Task TogglePlayPauseAsync() => ExecuteCommandAsync("toggle-play-pause", PlayerScripts.Toggle);
    public Task NextAsync() => ExecuteCommandAsync("next", PlayerScripts.Next);
    public Task PreviousAsync() => ExecuteCommandAsync("previous", PlayerScripts.Previous);
    public Task LikeAsync() => ExecuteCommandAsync("like", PlayerScripts.Like);
    public Task UnlikeAsync() => ExecuteCommandAsync("unlike", PlayerScripts.Unlike);
    public Task ToggleLikeAsync() => ExecuteCommandAsync("toggle-like", PlayerScripts.ToggleLike);
    public Task MuteAsync()
    {
        if (!UseKeyboardShortcuts)
            return ExecuteCommandAsync("mute", PlayerScripts.Mute);
        return GetStateSnapshot().IsMuted
            ? Task.CompletedTask
            : ExecuteKeyboardShortcutAsync("mute-shortcut", "m", "KeyM", 77);
    }

    public Task UnmuteAsync()
    {
        if (!UseKeyboardShortcuts)
            return ExecuteCommandAsync("unmute", PlayerScripts.Unmute);
        return GetStateSnapshot().IsMuted
            ? ExecuteKeyboardShortcutAsync("unmute-shortcut", "m", "KeyM", 77)
            : Task.CompletedTask;
    }

    public Task ToggleMuteAsync() =>
        UseKeyboardShortcuts
            ? ExecuteKeyboardShortcutAsync("toggle-mute-shortcut", "m", "KeyM", 77)
            : ExecuteCommandAsync("toggle-mute", PlayerScripts.ToggleMute);

    public Task SetVolumeAsync(int percentage)
    {
        if (percentage is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(percentage));
        EnsureReady("set-volume");
        _volumeTarget.Set(percentage);
        ScheduleVolumeFlush();
        return Task.CompletedTask;
    }

    public Task IncreaseVolumeAsync(int step)
    {
        if (step is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(step));
        if (UseKeyboardShortcuts)
            return ExecuteKeyboardShortcutAsync("volume-up-shortcut", "=", "Equal", 187);
        EnsureReady("increase-volume");
        _volumeTarget.ApplyDelta(step);
        ScheduleVolumeFlush();
        return Task.CompletedTask;
    }

    public Task DecreaseVolumeAsync(int step)
    {
        if (step is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(step));
        if (UseKeyboardShortcuts)
            return ExecuteKeyboardShortcutAsync("volume-down-shortcut", "-", "Minus", 189);
        EnsureReady("decrease-volume");
        _volumeTarget.ApplyDelta(-step);
        ScheduleVolumeFlush();
        return Task.CompletedTask;
    }

    public Task<PlayerState> GetStateAsync()
    {
        lock (_stateGate)
            return Task.FromResult(_state);
    }

    private bool UseKeyboardShortcuts =>
        settings.Current.VolumeControlMethod == VolumeControlMethod.KeyboardShortcuts;

    private void ScheduleVolumeFlush()
    {
        CancellationToken token;
        lock (_volumeGate)
        {
            _volumeCancellation?.Cancel();
            _volumeCancellation?.Dispose();
            _volumeCancellation = new CancellationTokenSource();
            token = _volumeCancellation.Token;
        }

        _ = FlushVolumeAsync(token);
    }

    private async Task FlushVolumeAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(50, token);
            var target = _volumeTarget.Current;
            await ExecuteCommandAsync($"set-volume-{target}", PlayerScripts.SetVolume(target), token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "A coalesced volume update failed");
        }
    }

    private async Task ExecuteKeyboardShortcutAsync(
        string commandName,
        string key,
        string code,
        int windowsVirtualKeyCode,
        CancellationToken cancellationToken = default)
    {
        var commandId = Interlocked.Increment(ref _commandSequence);
        var before = GetStateSnapshot();
        logger.LogInformation(
            "[CONTROL {CommandId}] START {CommandName}; Mechanism=keyboard-shortcut, " +
            "Key={Key}, Playing={Playing}, Muted={Muted}, Volume={Volume}",
            commandId, commandName, key, before.IsPlaying, before.IsMuted, before.Volume);

        EnsureReady(commandName);
        var dispatcher = _dispatcher!;
        var webView = _webView!;
        var keyDown = JsonSerializer.Serialize(new
        {
            type = "keyDown",
            key,
            code,
            windowsVirtualKeyCode,
            nativeVirtualKeyCode = windowsVirtualKeyCode,
            text = key,
            unmodifiedText = key
        });
        var keyUp = JsonSerializer.Serialize(new
        {
            type = "keyUp",
            key,
            code,
            windowsVirtualKeyCode,
            nativeVirtualKeyCode = windowsVirtualKeyCode
        });

        await dispatcher.InvokeAsync(
            async () =>
            {
                if (_disposed || webView.CoreWebView2 is null ||
                    !WebViewLifecycleService.IsTrustedMusicOrigin(webView.Source?.AbsoluteUri))
                    throw new PlayerNotReadyException(
                        $"Cannot execute '{commandName}': the YouTube Music page is unavailable.");

                await webView.CoreWebView2.ExecuteScriptAsync(
                    "if (document.activeElement instanceof HTMLElement) document.activeElement.blur();" +
                    "document.body?.focus();");
                await webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                    "Input.dispatchKeyEvent", keyDown);
                await webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                    "Input.dispatchKeyEvent", keyUp);
            },
            DispatcherPriority.Normal,
            cancellationToken).Task.Unwrap();

        logger.LogInformation(
            "[CONTROL {CommandId}] ACCEPTED {CommandName}; Mechanism=keyboard-shortcut, Key={Key}",
            commandId, commandName, key);
        _ = LogObservedOutcomeAsync(commandId, commandName, before);
    }

    private async Task ExecuteCommandAsync(
        string commandName, string script, CancellationToken cancellationToken = default)
    {
        var commandId = Interlocked.Increment(ref _commandSequence);
        var before = GetStateSnapshot();
        logger.LogInformation(
            "[CONTROL {CommandId}] START {CommandName}; Playing={Playing}, Muted={Muted}, " +
            "Volume={Volume}, Track={TrackTitle} / {TrackArtist}, CachedMediaDetected={MediaDetected}",
            commandId, commandName, before.IsPlaying, before.IsMuted, before.Volume,
            before.Track?.Title ?? "none", before.Track?.Artist ?? "none", _hasMedia);

        EnsureReady(commandName);
        var dispatcher = _dispatcher!;
        var webView = _webView!;
        logger.LogDebug("[CONTROL {CommandId}] Dispatching {CommandName} to the WebView UI thread",
            commandId, commandName);

        var result = await dispatcher.InvokeAsync(
            async () =>
            {
                if (_disposed || webView.CoreWebView2 is null ||
                    !WebViewLifecycleService.IsTrustedMusicOrigin(webView.Source?.AbsoluteUri))
                {
                    var reason = _disposed
                        ? "the controller is disposed"
                        : webView.CoreWebView2 is null
                            ? "WebView2 core is unavailable"
                            : $"current origin is not trusted ({webView.Source?.AbsoluteUri ?? "no URI"})";
                    logger.LogWarning(
                        "[CONTROL {CommandId}] UI-thread validation failed for {CommandName}: {Reason}",
                        commandId, commandName, reason);
                    throw new PlayerNotReadyException($"Cannot execute '{commandName}': {reason}.");
                }

                logger.LogDebug(
                    "[CONTROL {CommandId}] Executing JavaScript for {CommandName} at {SourceUri}",
                    commandId, commandName, webView.Source?.AbsoluteUri);
                return await webView.CoreWebView2.ExecuteScriptAsync(script);
            },
            DispatcherPriority.Normal,
            cancellationToken).Task.Unwrap();

        logger.LogDebug("[CONTROL {CommandId}] RAW SCRIPT RESULT for {CommandName}: {ScriptResult}",
            commandId, commandName, result);

        ScriptResult? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ScriptResult>(result, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex,
                "[CONTROL {CommandId}] Malformed JSON for {CommandName}: {ScriptResult}",
                commandId, commandName, result);
            throw new InvalidOperationException(
                "The YouTube Music command returned an invalid response.", ex);
        }

        logger.LogInformation(
            "[CONTROL {CommandId}] INTERNAL RESULT {CommandName}: Ok={Ok}, Error={Error}, " +
            "Mechanism={Mechanism}, Control={Control}, Selector={Selector}, Element={TagName}#{ElementId}, " +
            "Class={ClassName}, PlayerMethod={PlayerMethod}, Detail={Detail}, Candidates={CandidateCount}",
            commandId, commandName, parsed?.Ok, parsed?.Error ?? "none",
            parsed?.Mechanism ?? "direct-media-script", parsed?.Control ?? "none",
            parsed?.Selector ?? "none", parsed?.TagName ?? "none", parsed?.Id ?? "none",
            parsed?.ClassName ?? "none", parsed?.Method ?? "none", parsed?.Detail ?? "none",
            parsed?.CandidateCount);

        if (parsed?.Ok == true)
        {
            logger.LogInformation("[CONTROL {CommandId}] ACCEPTED {CommandName} by the YouTube Music page",
                commandId, commandName);
            _ = LogObservedOutcomeAsync(commandId, commandName, before);
            return;
        }

        if (parsed?.Error is "player-not-ready" or "untrusted-origin")
        {
            logger.LogWarning(
                "[CONTROL {CommandId}] REJECTED {CommandName}: Error={Error}, Detail={Detail}, Selector={Selector}",
                commandId, commandName, parsed.Error, parsed.Detail ?? "none", parsed.Selector ?? "none");
            throw new PlayerNotReadyException(
                $"Cannot execute '{commandName}': the page reported '{parsed.Error}' " +
                $"({parsed.Detail ?? "no details"}).");
        }

        logger.LogError("[CONTROL {CommandId}] FAILED {CommandName}: {Error}",
            commandId, commandName, parsed?.Error ?? "missing-result");
        throw new InvalidOperationException("The YouTube Music command failed.");
    }

    private async Task LogObservedOutcomeAsync(
        long commandId, string commandName, PlayerState before)
    {
        try
        {
            var delay = commandName is "next" or "previous" ? 1500 : 350;
            await Task.Delay(delay);
            if (_disposed)
                return;

            var after = GetStateSnapshot();
            logger.LogInformation(
                "[CONTROL {CommandId}] OBSERVED {CommandName} after {DelayMilliseconds} ms: " +
                "StateChanged={StateChanged}, Playing {BeforePlaying}->{AfterPlaying}, " +
                "Muted {BeforeMuted}->{AfterMuted}, Volume {BeforeVolume}->{AfterVolume}, " +
                "Track {BeforeTrack}->{AfterTrack}",
                commandId, commandName, delay, before != after,
                before.IsPlaying, after.IsPlaying, before.IsMuted, after.IsMuted,
                before.Volume, after.Volume, before.Track?.Title ?? "none", after.Track?.Title ?? "none");
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[CONTROL {CommandId}] Could not record observed outcome for {CommandName}",
                commandId, commandName);
        }
    }

    private PlayerState GetStateSnapshot()
    {
        lock (_stateGate)
            return _state;
    }

    private void EnsureReady(string commandName)
    {
        var reason = _disposed
            ? "the controller is disposed"
            : _webView is null
                ? "WebView2 has not been assigned"
                : _dispatcher is null
                    ? "the UI dispatcher is unavailable"
                    : null;

        if (reason is null)
        {
            logger.LogDebug(
                "Player readiness check passed for command {CommandName}; cached MediaDetected={MediaDetected}",
                commandName, _hasMedia);
            return;
        }

        logger.LogWarning(
            "Player readiness check failed for command {CommandName}: {Reason}. " +
            "Disposed={Disposed}, WebViewAssigned={WebViewAssigned}, " +
            "DispatcherAssigned={DispatcherAssigned}, MediaDetected={MediaDetected}",
            commandName, reason, _disposed, _webView is not null, _dispatcher is not null, _hasMedia);
        throw new PlayerNotReadyException($"Cannot execute '{commandName}': {reason}.");
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs eventArgs)
    {
        var allowed = WebViewLifecycleService.IsAllowedUri(eventArgs.Uri);
        logger.LogInformation("YouTube Music navigation starting for {Uri}; allowed={Allowed}",
            eventArgs.Uri, allowed);
        if (allowed)
        {
            _hasMedia = false;
            logger.LogDebug("Player media readiness reset while navigation is in progress");
            return;
        }

        eventArgs.Cancel = true;
        logger.LogWarning("Blocked in-app navigation to {Uri}; opening externally", eventArgs.Uri);
        OpenExternal(eventArgs.Uri);
    }

    private void OnFrameNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs eventArgs)
    {
        var allowed = WebViewLifecycleService.IsAllowedUri(eventArgs.Uri);
        logger.LogDebug("WebView frame navigation starting for {Uri}; allowed={Allowed}",
            eventArgs.Uri, allowed);
        if (!allowed)
            eventArgs.Cancel = true;
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs eventArgs)
    {
        eventArgs.Handled = true;
        var allowed = WebViewLifecycleService.IsAllowedUri(eventArgs.Uri);
        logger.LogDebug("WebView new-window request for {Uri}; allowed in app={Allowed}",
            eventArgs.Uri, allowed);
        if (allowed)
            _webView?.CoreWebView2.Navigate(eventArgs.Uri);
        else
            OpenExternal(eventArgs.Uri);
    }

    private static void OpenExternal(string? uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) ||
            parsed.Scheme is not ("https" or "http"))
            return;
        Process.Start(new ProcessStartInfo(parsed.AbsoluteUri) { UseShellExecute = true });
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs eventArgs)
    {
        if (!WebViewLifecycleService.IsTrustedMusicOrigin(eventArgs.Source))
        {
            logger.LogWarning("Ignored WebView message from untrusted source {MessageSource}",
                eventArgs.Source);
            return;
        }

        var currentSource = _webView?.Source?.AbsoluteUri;
        if (!WebViewLifecycleService.IsTrustedMusicOrigin(currentSource))
        {
            logger.LogWarning(
                "Ignored WebView message because the current page is not trusted: {CurrentSource}",
                currentSource ?? "no URI");
            return;
        }

        var rawMessage = eventArgs.WebMessageAsJson;
        logger.LogDebug("WebView message received from {MessageSource}: {WebMessage}",
            eventArgs.Source, rawMessage);

        try
        {
            var message = JsonSerializer.Deserialize<PlayerStateMessage>(rawMessage, JsonOptions);
            if (message?.Type != "player-state")
            {
                logger.LogWarning("Ignored WebView message with unexpected type {MessageType}",
                    message?.Type ?? "missing");
                return;
            }

            if (message.Volume is < 0 or > 100)
            {
                logger.LogWarning("Ignored player-state message with invalid volume {Volume}",
                    message.Volume);
                return;
            }

            var track = ValidateTrack(message.Track);
            var next = new PlayerState(message.IsPlaying, message.IsMuted, message.Volume, message.IsLiked, track);
            lock (_stateGate)
            {
                _state = next;
                _hasMedia = message.HasMedia;
            }

            logger.LogDebug(
                "Player state updated: MediaDetected={MediaDetected}, Playing={Playing}, " +
                "Muted={Muted}, Volume={Volume}, Liked={Liked}, Title={Title}, Artist={Artist}, Album={Album}",
                message.HasMedia, message.IsPlaying, message.IsMuted, message.Volume, message.IsLiked,
                track?.Title ?? "none", track?.Artist ?? "none", track?.Album ?? "none");
            _volumeTarget.Set(message.Volume);
            StateChanged?.Invoke(this, next);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Ignored a malformed WebView player-state message: {WebMessage}",
                rawMessage);
        }
    }

    private static TrackInfo? ValidateTrack(TrackMessage? track)
    {
        if (track is null)
            return null;
        static string? Clean(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value[..Math.Min(value.Length, 1000)];
        var artwork = Uri.TryCreate(track.ArtworkUrl, UriKind.Absolute, out var uri) &&
                      uri.Scheme == Uri.UriSchemeHttps ? uri.AbsoluteUri : null;
        return new TrackInfo(Clean(track.Title), Clean(track.Artist), Clean(track.Album), artwork);
    }

    private static void ConfigureSecurity(CoreWebView2 core)
    {
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.IsWebMessageEnabled = true;
        core.Settings.IsScriptEnabled = true;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDefaultScriptDialogsEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
#if DEBUG
        core.Settings.AreDevToolsEnabled = true;
#else
        core.Settings.AreDevToolsEnabled = false;
#endif
    }

    public void Dispose()
    {
        _disposed = true;
        _hasMedia = false;
        logger.LogDebug("Disposing YouTube Music WebView controller");
        lock (_volumeGate)
        {
            _volumeCancellation?.Cancel();
            _volumeCancellation?.Dispose();
            _volumeCancellation = null;
        }
    }

    private sealed record ScriptResult(
        bool Ok,
        string? Error,
        string? Mechanism,
        string? Control,
        string? Selector,
        string? TagName,
        string? Id,
        string? ClassName,
        string? Detail,
        string? Method,
        int? CandidateCount);
    private sealed record PlayerStateMessage(
        string? Type,
        bool IsPlaying,
        bool IsMuted,
        int Volume,
        bool? IsLiked,
        bool HasMedia,
        TrackMessage? Track);
    private sealed record TrackMessage(string? Title, string? Artist, string? Album, string? ArtworkUrl);
}
