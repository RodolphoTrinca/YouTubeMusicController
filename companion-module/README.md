# Bitfocus Companion Module: YouTube Music Controller

Controls the local .NET WebView2 **YtMusicController** app from Bitfocus Companion. It provides playback, YouTube Music player volume, mute, Like state, track metadata, live feedback, and D200X-oriented presets. It never changes Windows master volume and does not require YTMDesktop.

## Integration

- Commands use bearer-authenticated HTTP endpoints on the controller's loopback API.
- The initial player state comes from `GET /api/player/status`.
- Live changes arrive through `ws://127.0.0.1:PORT/api/player/events`.
- The client reconnects automatically if the app starts late or restarts.
- The module opens no server and never logs the bearer token.

The WebSocket updates Companion when playback, track metadata, volume, mute, or Like state changes inside YouTube Music—not only after a Companion action.

## Requirements

- YtMusicController build containing the `/api/player/events` WebSocket and Like endpoints
- Bitfocus Companion 5.x
- Node.js 22.20+ only when building the module from source
- The Ulanzi D200X surface module for physical D200X input/output

## Install

Import `ytmdesktop-0.3.3.tgz` through Companion's module import interface, or place this repository under a Companion developer-modules parent directory and configure that parent under **Settings → Advanced → Developer**.

The module keeps the `ytmdesktop` identifier for compatibility with the earlier prototype, but connects to YtMusicController—not the separate YTMDesktop application.

## Connection configuration

1. Start YtMusicController and play a track.
2. Open its **Settings** window.
3. Copy the **Active API URL** and note its port.
4. Copy the **Bearer token**.
5. Add **YouTube Music Controller** as a Companion connection.
6. Use host `127.0.0.1`, the active port, and the copied token.
7. Save and wait for status **OK**.

If YtMusicController selects another port because `38472` is occupied, update the module connection with the displayed active port.

## Actions

- Play, Pause, Play/Pause toggle
- Previous and Next
- Volume Up and Volume Down
- Set exact player volume (0–100)
- Mute, Unmute, Toggle Mute
- Like, Unlike, Toggle Like

Relative volume actions follow the control method selected in YtMusicController Settings. Keyboard mode shows YouTube Music's native visual volume overlay; Player API mode uses the configured precise step. Exact volume always uses the player API.

## Feedbacks

- Playing
- Paused
- Muted
- Current track is liked
- Volume equals, exceeds, falls below, or is within a configured range
- D200X clock / now-playing display (album art and metadata while playing)
- Long track and artist names scroll across the wide display instead of being permanently truncated

## Variables

- `player_state`
- `is_playing`
- `track_title`
- `track_artist`
- `track_album`
- `artwork_url`
- `volume`
- `is_muted`
- `is_liked` (`true`, `false`, or `unknown`)
- `like_symbol` (`♥`, `♡`, or `·`)
- `clock` (module-refreshed local time)

## Presets

Previous, Play/Pause, Next, Volume Up, Volume Down, Mute, Volume Encoder, Like, and D200X Clock / Now Playing.

## Recommended Ulanzi D200X Setup

```text
┌──────────┬────────────┬──────────┐
│ Previous │ Play/Pause │ Next     │
└──────────┴────────────┴──────────┘

Wide display above encoders:
Idle                 Clock
Playing              Album art + title + artist + ♥/♡

Encoder:
CW    → Volume Up
CCW   → Volume Down
Press → Toggle Mute

Button:
Press → Toggle Like
Color/text feedback → current Like state
```

In the D200X surface configuration, set **Small window mode** to **Full-width button (no clock overlay)**. Firmware clock modes are always drawn above Companion content, so they cannot be conditionally hidden.

Place the **D200X clock / now playing** preset on the wide Companion grid control above the encoders. Companion displays its clock while idle and automatically replaces it with album artwork, title, artist, and Like state while playing. If artwork is unavailable, the track text still appears. The Like preset can go on any regular key.
Long title and artist values scroll continuously in the available text area.
While idle, the module publishes its own `clock` variable once per second so the wide display refreshes reliably.

## Build and test

```sh
pnpm install
pnpm build
pnpm test
pnpm lint
pnpm package
```

The importable package is named `ytmdesktop-0.3.3.tgz`.

## Troubleshooting

- **Bad configuration:** copy the current bearer token from YtMusicController Settings.
- **Authentication failure:** the token was regenerated; replace it in Companion.
- **Connection failure:** confirm the app is running and the connection uses its active port.
- **State does not update:** verify local WebSocket traffic is not blocked, then disable/re-enable the connection.
- **Like shows unknown:** start a normal signed-in track and wait for YouTube Music's player-bar Like control to load. Live DOM behavior must be checked against the current YouTube Music site.
- **Clock covers track information:** set the D200X surface Small window mode to **Full-width button (no clock overlay)**, then reapply the new clock/now-playing preset.
- **Album art is missing:** wait for YouTube Music metadata to load. The module accepts only HTTPS artwork from YouTube/Google image hosts and falls back to text safely.
