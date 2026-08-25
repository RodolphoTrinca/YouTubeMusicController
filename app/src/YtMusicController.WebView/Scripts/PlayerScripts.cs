using System.Globalization;
using System.Text.Json;

namespace YtMusicController.WebView.Scripts;

internal static class PlayerScripts
{
    public const string StateObserver = """
(() => {
  if (location.origin !== 'https://music.youtube.com') return;
  if (window.__ytMusicControllerObserverInstalled) return;
  window.__ytMusicControllerObserverInstalled = true;

  let boundMedia;
  let lastState = '';

  const findMedia = () => document.querySelector('video, audio');
  const findPlayer = () => document.querySelector('#movie_player');
  const findLikeRenderer = () => document.querySelector('ytmusic-player-bar ytmusic-like-button-renderer');
  const readLiked = () => {
    const renderer = findLikeRenderer();
    if (!renderer) return null;
    const status = renderer.likeStatus ?? renderer.data?.likeStatus ?? renderer.__data?.data?.likeStatus;
    if (status === 'LIKE') return true;
    if (status === 'INDIFFERENT' || status === 'DISLIKE') return false;
    const button = renderer.querySelector(
      '#button-shape-like button, #button-shape-like, button[aria-pressed]');
    const pressed = button?.getAttribute('aria-pressed');
    return pressed === 'true' ? true : pressed === 'false' ? false : null;
  };
  const clean = value => typeof value === 'string' ? value.slice(0, 1000) : null;
  const publish = () => {
    if (location.origin !== 'https://music.youtube.com') return;
    const media = findMedia();
    const player = findPlayer();
    let volume = media ? Math.round(media.volume * 100) : 100;
    let isMuted = !!media && media.muted;
    try {
      if (player && typeof player.getVolume === 'function')
        volume = player.getVolume();
      if (player && typeof player.isMuted === 'function')
        isMuted = player.isMuted();
    } catch {
      // The media element remains a safe fallback while the player API initializes.
    }
    const metadata = navigator.mediaSession?.metadata ?? null;
    const artwork = metadata && Array.isArray(metadata.artwork) && metadata.artwork.length
      ? metadata.artwork[metadata.artwork.length - 1].src : null;
    const state = {
      type: 'player-state',
      isPlaying: !!media && !media.paused && !media.ended,
      isMuted: !!isMuted,
      volume: Math.max(0, Math.min(100, Math.round(Number(volume) || 0))),
      isLiked: readLiked(),
      hasMedia: !!media,
      track: metadata ? {
        title: clean(metadata.title),
        artist: clean(metadata.artist),
        album: clean(metadata.album),
        artworkUrl: clean(artwork)
      } : null
    };
    const serialized = JSON.stringify(state);
    if (serialized !== lastState) {
      lastState = serialized;
      window.chrome.webview.postMessage(state);
    }
  };

  const bindMedia = () => {
    const media = findMedia();
    if (media === boundMedia) return;
    boundMedia = media;
    if (media) {
      ['play', 'pause', 'volumechange', 'loadedmetadata', 'emptied', 'ended']
        .forEach(name => media.addEventListener(name, publish, { passive: true }));
    }
    publish();
  };

  const start = () => {
    if (!document.documentElement) {
      setTimeout(start, 0);
      return;
    }

    new MutationObserver(() => { bindMedia(); publish(); })
      .observe(document.documentElement, { childList: true, subtree: true, attributes: true });
    bindMedia();
    setInterval(() => { bindMedia(); publish(); }, 2000);
  };

  if (document.readyState === 'loading')
    document.addEventListener('DOMContentLoaded', start, { once: true });
  else
    start();
})();
""";

    public static readonly string Play = MediaCommand("void media.play().catch(() => {});");
    public static readonly string Pause = MediaCommand("media.pause();");
    public static readonly string Toggle = MediaCommand("if (media.paused) void media.play().catch(() => {}); else media.pause();");
    public static readonly string Mute = PlayerVolumeCommand(
        "mute", "player.mute();", "media.muted = true;");
    public static readonly string Unmute = PlayerVolumeCommand(
        "unmute", "player.unMute();", "media.muted = false;");
    public static readonly string ToggleMute = PlayerVolumeCommand(
        "toggle-mute",
        "if (typeof player.isMuted === 'function' && player.isMuted()) player.unMute(); else player.mute();",
        "media.muted = !media.muted;");

    public static readonly string Next = ButtonCommand("next", "nextVideo", new[]
    {
        "ytmusic-player-bar .next-button.ytmusic-player-bar",
        "ytmusic-player-bar #next-button",
        "ytmusic-player-bar .next-button",
        ".next-button.ytmusic-player-bar"
    });

    public static readonly string Previous = ButtonCommand("previous", "previousVideo", new[]
    {
        "ytmusic-player-bar .previous-button.ytmusic-player-bar",
        "ytmusic-player-bar #previous-button",
        "ytmusic-player-bar .previous-button",
        ".previous-button.ytmusic-player-bar"
    });

    public static readonly string Like = LikeCommand("like", true);
    public static readonly string Unlike = LikeCommand("unlike", false);
    public static readonly string ToggleLike = LikeCommand("toggle-like", null);

    public static string SetVolume(int percentage)
    {
        var clamped = Math.Clamp(percentage, 0, 100);
        var fraction = (clamped / 100d).ToString("0.00", CultureInfo.InvariantCulture);
        return PlayerVolumeCommand(
            "volume", $"player.setVolume({clamped});", $"media.volume = {fraction};");
    }

    private static string PlayerVolumeCommand(
        string controlName, string playerCommand, string mediaFallback)
    {
        var serializedControlName = JsonSerializer.Serialize(controlName);
        var serializedMethod = JsonSerializer.Serialize(
            controlName == "volume" ? "setVolume" : controlName);
        return $$"""
(() => {
  if (location.origin !== 'https://music.youtube.com') return { ok: false, error: 'untrusted-origin' };
  const player = document.querySelector('#movie_player');
  try {
    if (player && typeof player.setVolume === 'function' &&
        typeof player.mute === 'function' && typeof player.unMute === 'function') {
      {{playerCommand}}
      return {
        ok: true,
        mechanism: 'movie-player-api',
        control: {{serializedControlName}},
        method: {{serializedMethod}}
      };
    }

    const media = document.querySelector('video, audio');
    if (!media) return { ok: false, error: 'player-not-ready' };
    {{mediaFallback}}
    return {
      ok: true,
      mechanism: 'direct-media-fallback',
      control: {{serializedControlName}}
    };
  } catch (error) {
    return {
      ok: false,
      error: 'command-failed',
      detail: error instanceof Error ? error.message : String(error),
      control: {{serializedControlName}}
    };
  }
})()
""";
    }

    private static string MediaCommand(string command) => $$"""
(() => {
  if (location.origin !== 'https://music.youtube.com') return { ok: false, error: 'untrusted-origin' };
  const media = document.querySelector('video, audio');
  if (!media) return { ok: false, error: 'player-not-ready' };
  try {
    {{command}}
    return { ok: true };
  } catch {
    return { ok: false, error: 'command-failed' };
  }
})()
""";

    private static string ButtonCommand(
        string controlName, string playerMethod, IEnumerable<string> selectors)
    {
        var selectorList = JsonSerializer.Serialize(selectors);
        var serializedControlName = JsonSerializer.Serialize(controlName);
        var serializedPlayerMethod = JsonSerializer.Serialize(playerMethod);
        return $$"""
(() => {
  if (location.origin !== 'https://music.youtube.com') return { ok: false, error: 'untrusted-origin' };
  const selectors = {{selectorList}};
  const candidates = selectors
    .map(selector => ({ selector, element: document.querySelector(selector) }))
    .filter(candidate => !!candidate.element);
  const inspectedCandidates = candidates.map(candidate => {
    const element = candidate.element;
    const style = window.getComputedStyle(element);
    return {
      selector: candidate.selector,
      visible: style.display !== 'none' && style.visibility !== 'hidden' &&
        element.getClientRects().length > 0,
      disabled: !!element.disabled || element.getAttribute('aria-disabled') === 'true',
      tagName: element.tagName,
      id: element.id || null,
      className: String(element.className || '')
    };
  });
  const visibleIndex = inspectedCandidates.findIndex(candidate => candidate.visible);
  const match = visibleIndex >= 0 ? candidates[visibleIndex] : candidates[0];

  if (match) {
    const button = match.element;
    const selected = inspectedCandidates[visibleIndex >= 0 ? visibleIndex : 0];
    if (selected.disabled) {
      return {
        ok: false,
        error: 'player-not-ready',
        detail: 'control-disabled',
        control: {{serializedControlName}},
        selector: match.selector,
        candidateCount: candidates.length,
        candidates: inspectedCandidates
      };
    }

    button.click();
    return {
      ok: true,
      mechanism: 'player-bar-button',
      control: {{serializedControlName}},
      selector: match.selector,
      tagName: button.tagName,
      id: button.id || null,
      className: String(button.className || ''),
      candidateCount: candidates.length,
      candidates: inspectedCandidates
    };
  }

  const player = document.querySelector('#movie_player');
  const method = {{serializedPlayerMethod}};
  if (player && typeof player[method] === 'function') {
    player[method]();
    return {
      ok: true,
      mechanism: 'movie-player-api',
      control: {{serializedControlName}},
      method,
      candidateCount: 0,
      candidates: []
    };
  }

  return {
    ok: false,
    error: 'player-not-ready',
    detail: 'control-not-found',
    control: {{serializedControlName}},
    candidateCount: 0,
    candidates: [],
    selectors
  };
})()
""";
    }

    private static string LikeCommand(string controlName, bool? desiredState)
    {
        var serializedControlName = JsonSerializer.Serialize(controlName);
        var serializedDesiredState = desiredState.HasValue
            ? (desiredState.Value ? "true" : "false")
            : "null";
        return $$"""
(() => {
  if (location.origin !== 'https://music.youtube.com') return { ok: false, error: 'untrusted-origin' };
  const renderer = document.querySelector('ytmusic-player-bar ytmusic-like-button-renderer');
  const selectors = [
    'ytmusic-player-bar ytmusic-like-button-renderer #button-shape-like button',
    'ytmusic-player-bar ytmusic-like-button-renderer #button-shape-like',
    'ytmusic-player-bar ytmusic-like-button-renderer button[aria-pressed]',
    'ytmusic-player-bar ytmusic-like-button-renderer button'
  ];
  const readLiked = () => {
    if (!renderer) return null;
    const status = renderer.likeStatus ?? renderer.data?.likeStatus ?? renderer.__data?.data?.likeStatus;
    if (status === 'LIKE') return true;
    if (status === 'INDIFFERENT' || status === 'DISLIKE') return false;
    const button = renderer.querySelector('#button-shape-like button, #button-shape-like, button[aria-pressed]');
    const pressed = button?.getAttribute('aria-pressed');
    return pressed === 'true' ? true : pressed === 'false' ? false : null;
  };
  const current = readLiked();
  const desired = {{serializedDesiredState}};
  if (desired !== null && current === desired) {
    return { ok: true, mechanism: 'already-in-state', control: {{serializedControlName}}, detail: String(current) };
  }
  const candidates = selectors
    .map(selector => ({ selector, element: document.querySelector(selector) }))
    .filter(candidate => !!candidate.element);
  const match = candidates.find(candidate => {
    const style = window.getComputedStyle(candidate.element);
    return style.display !== 'none' && style.visibility !== 'hidden' &&
      candidate.element.getClientRects().length > 0;
  }) ?? candidates[0];
  if (!match) {
    return {
      ok: false,
      error: 'player-not-ready',
      detail: 'like-control-not-found',
      control: {{serializedControlName}},
      candidateCount: 0,
      selectors
    };
  }
  const button = match.element;
  if (button.disabled || button.getAttribute('aria-disabled') === 'true') {
    return {
      ok: false,
      error: 'player-not-ready',
      detail: 'like-control-disabled',
      control: {{serializedControlName}},
      selector: match.selector,
      candidateCount: candidates.length
    };
  }
  button.click();
  return {
    ok: true,
    mechanism: 'player-bar-like-button',
    control: {{serializedControlName}},
    selector: match.selector,
    tagName: button.tagName,
    id: button.id || null,
    className: String(button.className || ''),
    detail: current === null ? 'previous-state-unknown' : String(current),
    candidateCount: candidates.length
  };
})()
""";
    }
}
