# YouTube Music Controller

Controls the local YtMusicController WebView2 app. Commands use its authenticated localhost HTTP API; playback, metadata, volume, mute, and Like feedback arrive through WebSocket.

## Setup

1. Start YtMusicController and play a track.
2. Open its Settings window.
3. Copy the Active API URL and bearer token.
4. Configure this connection with `127.0.0.1`, the active URL's port, and that token.
5. Wait for status **OK**.

If the app automatically selected another port, use that port instead of `38472`.

## Recommended Ulanzi D200X Setup

- Previous, Play/Pause, and Next presets on three regular keys
- Volume Encoder preset on a knob: clockwise Volume Up, counterclockwise Volume Down, press Toggle Mute
- Like preset on a regular key for `♡`/`♥` feedback
- D200X Wide Track Display preset on the wide control above the encoders

The wide display shows `track_title`, `track_artist`, and the current Like symbol. Volume actions control YouTube Music only and follow the method selected in YtMusicController Settings.
Long title and artist values scroll automatically while the track is playing.
The module-provided clock variable refreshes the idle display once per second.

## Troubleshooting

- Authentication failure means the token in Companion no longer matches the app.
- Connection failure usually means the app is stopped or its active port changed.
- Like state can remain unknown until a signed-in track and YouTube Music's Like control are loaded.
