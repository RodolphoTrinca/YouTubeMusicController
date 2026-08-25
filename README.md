# YtMusicController

A lightweight Windows YouTube Music application built with .NET 8, WPF, and Microsoft WebView2. It displays the official `music.youtube.com` site and exposes a bearer-authenticated loopback API for Bitfocus Companion.

Playback, Like, mute, and volume commands target the player inside the app. Windows master volume is never changed, and the separate YTMDesktop application is not required.

## Architecture

```text
Ulanzi D200X surface → Bitfocus Companion
                              │
             HTTP commands ──┤
             WebSocket state ←┘
                              │
                    YtMusicController
                              │
                    music.youtube.com
```

HTTP is used for explicit commands. The authenticated WebSocket broadcasts playback, track metadata, volume, mute, and Like changes, including changes initiated directly in YouTube Music.

## Repository layout

```text
YouTubeMusicController/
├── app/
│   ├── src/                 # .NET application projects
│   └── tests/               # .NET test projects
├── companion-module/        # Bitfocus Companion connection module (TypeScript)
├── YtMusicController.sln
├── build.cmd
└── README.md
```

The reusable Ulanzi D200X surface integration is maintained separately in the sibling `companion-surface-ulanzi-d200x` repository.

## Requirements

- Windows 10 or Windows 11, x64
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [Microsoft Edge WebView2 Evergreen Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
- Bitfocus Companion 5.x
- The local YouTube Music Controller Companion connection module for variables/feedback
- The Ulanzi D200X Companion surface module for physical D200X controls

Generic HTTP remains usable for command-only setups, but the dedicated connection module is required for live Like and track-display feedback.

## Run the packaged build

1. Extract `YtMusicController-win-x64.zip`.
2. Run `YtMusicController.exe`.
3. Sign in to Google inside the application.
4. Start a track.
5. Open Settings and copy the Active API URL and bearer token.
6. Choose **Player API** for precise volume steps or **Keyboard shortcuts** for YouTube Music's native visual volume overlay.

The normal API URL is `http://127.0.0.1:38472`. If that port is occupied, the app selects and persists an available loopback port and displays the URL to use.

The persistent WebView profile and DPAPI-protected settings are stored below `%LOCALAPPDATA%\YtMusicController`.

## Debug logging

Open Settings, enable **Show debug log window**, and click **Open now** or Save. Serilog writes structured diagnostics to the terminal-style panel and to daily files under:

```text
%LOCALAPPDATA%\YtMusicController\Logs
```

Tokens are never written to logs.

## Build from source

From Command Prompt or File Explorer, run:

```cmd
build.cmd
```

For automation without the final pause:

```cmd
build.cmd --no-pause
```

The script restores locked dependencies, builds Release, runs tests, publishes `win-x64`, and creates `YtMusicController-win-x64.zip` in the parent outputs directory.

## Local API

Every route binds only to `127.0.0.1` and requires:

```http
Authorization: Bearer YOUR_TOKEN
```

| Method | Path | Operation |
|---|---|---|
| POST | `/api/player/play` | Play |
| POST | `/api/player/pause` | Pause |
| POST | `/api/player/toggle` | Toggle play/pause |
| POST | `/api/player/next` | Next track |
| POST | `/api/player/previous` | Previous track |
| POST | `/api/player/like` | Like current track |
| POST | `/api/player/unlike` | Unlike current track |
| POST | `/api/player/toggle-like` | Toggle current track Like |
| POST | `/api/volume/up` | Configured player step or one keyboard shortcut increment |
| POST | `/api/volume/down` | Configured player step or one keyboard shortcut decrement |
| PUT | `/api/volume/{0-100}` | Set exact player volume |
| POST | `/api/volume/mute` | Mute |
| POST | `/api/volume/unmute` | Unmute |
| POST | `/api/volume/toggle-mute` | Toggle mute |
| GET | `/api/player/status` | Current state and metadata |
| WebSocket | `/api/player/events` | Initial and changed player-state snapshots |

The WebSocket client uses the same token during its authenticated loopback handshake. The supplied Companion module handles this automatically.

Example command:

```powershell
curl.exe -X POST -H "Authorization: Bearer YOUR_TOKEN" ACTIVE_API_URL/api/player/toggle-like
```

Expected HTTP failures use problem JSON: 400 invalid request, 401 invalid token, 403 non-loopback caller, 409 player/control not ready, and 500 unexpected failure.

## Companion setup

1. Build/import the module from `companion-module`, or load that directory as a Companion developer module.
2. Add the **YouTube Music Controller** connection.
3. Enter host `127.0.0.1`.
4. Copy the active port and bearer token from this app's Settings window.
5. Save and wait for the connection to show **OK**.
6. Add the supplied transport, encoder, Like, and track-display presets.

If this app later chooses another port, update the Companion connection with the displayed active port.

## Recommended Ulanzi D200X Setup

```text
┌──────────┬────────────┬──────────┐
│ Previous │ Play/Pause │ Next     │
└──────────┴────────────┴──────────┘

Wide display above encoders:
Song title
Artist name          ♥/♡

Encoder:
CW    → Volume Up
CCW   → Volume Down
Press → Toggle Mute

Regular button:
Press → Toggle Like
Feedback → ♡ / ♥ and color
```

Place the **D200X wide track display** preset on the wide grid control above the encoders. The existing D200X surface module already renders this wide Companion control; no additional HID protocol change is necessary.

## Manual validation

1. Launch the app, sign in, and play a normal track.
2. Verify Previous, Play/Pause, and Next from Companion.
3. Rotate the D200X encoder and confirm YouTube Music volume changes while Windows master volume does not.
4. In Keyboard mode, confirm YouTube Music's native volume overlay appears.
5. Press the Like key; confirm the player changes and the D200X feedback becomes `♥`.
6. Like/unlike the track directly in YouTube Music; confirm Companion updates without another Companion action.
7. Change tracks; confirm the wide display updates title and artist.
8. Stop and restart YtMusicController; confirm the Companion connection reconnects.

Google authentication and the live YouTube Music DOM require manual validation because automated tests do not use a Google account.

## Security

- Kestrel binds to IPv4 loopback only, never `0.0.0.0`.
- A random 256-bit bearer token protects HTTP and WebSocket access.
- The token is DPAPI-protected for the current Windows user and never logged.
- No arbitrary JavaScript, shell, filesystem, or generic command API is exposed.
- WebView messages are accepted only from the live `music.youtube.com` origin and validated defensively.
- Google credentials and cookies stay inside the WebView profile.

## Troubleshooting

**Like returns 409 or remains unknown:** Sign in, start a regular track, and wait for YouTube Music's player-bar Like control to load. Enable verbose logs and look for `like-control-not-found` or the selected control details. YouTube's internal DOM can change over time.

**Companion authentication failure:** Copy the current token from Settings. Regenerating it immediately invalidates the previous Companion configuration.

**Connection failure:** Confirm the app is running and Companion uses the green Active API URL's port.

**Wide display is blank:** Confirm the Companion connection is OK and place the track-display preset on the D200X wide grid control.

**Volume lacks native visual feedback:** Select **Keyboard shortcuts (native feedback)** in app Settings. Exact set-volume continues to use the player API.

**Next/Previous or Like stopped working:** These controls use centralized, defensive selectors in `PlayerScripts.cs`. The verbose control result identifies the selector and mechanism used, or reports that the current YouTube UI control was not found.
