# Integration research and decisions

Research was performed in August 2026 before implementation.

## Selected approach

- Host: .NET 8 WPF using the Generic Host for dependency injection, logging, and API lifetime.
- Logging: Serilog 4.4.0 routes `ILogger` events to a bounded in-memory viewer and seven daily rolling files. Serilog.Extensions.Hosting 8.0.0 matches the .NET 8 host; bearer values are redacted before entering the viewer.
- Browser: Microsoft WebView2 Evergreen, package version 1.0.4078.44.
- Control: standard HTMLMediaElement for play, pause, internal volume, mute, and playback events.
- Metadata: navigator.mediaSession.metadata when available. Metadata failure is non-fatal.
- Next/previous: no public browser API lets host code invoke a page's registered Media Session action handlers. YouTube Music fallback selectors are isolated in one PlayerScripts adapter.
- Synchronization: media events plus a DOM observer publish strict player-state messages. A two-second rediscovery check handles replacement of the media element without aggressive state polling.
- Companion: the current official Generic HTTP module supports POST/PUT and JSON-configured headers, so a custom Companion module is unnecessary.

## Authentication

There is no YTMDesktop authentication. The user signs into Google directly inside the official page. WebView2 stores the session in a dedicated profile; application code never receives Google credentials.

The local API uses a separate random 256-bit bearer token encrypted with Windows DPAPI for the current user.

## Security basis

The implementation follows current Microsoft guidance by validating origins, restricting navigation, minimizing web/native exposure, and using an explicit per-user data folder. It checks both message source and current origin, exposes no host object, accepts no commands from page messages, compiles in all scripts, denies permissions/downloads, and disables Release DevTools.

References:

- [WebView2 security guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security)
- [WebView2 user-data folders](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder)
- [WPF Generic Host guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/how-to-use-host-builder)
- [Kestrel endpoints](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-8.0)
- [WebView2 NuGet package](https://www.nuget.org/packages/Microsoft.Web.WebView2)
- [Bitfocus Generic HTTP module](https://github.com/bitfocus/companion-module-generic-http)

## Live YouTube Music validation boundary

Automated tests do not sign into a Google account. The solution and API are build-tested, and player-independent behavior is covered with fakes, but the authenticated page must be manually verified with the README checklist. Confirm that the current page exposes its video element and the centralized next/previous selectors. This boundary avoids storing test credentials or weakening Google authentication.
