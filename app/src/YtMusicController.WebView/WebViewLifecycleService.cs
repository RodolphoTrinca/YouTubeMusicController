using System.IO;

namespace YtMusicController.WebView;

public static class WebViewLifecycleService
{
    public static readonly Uri YouTubeMusicUri = new("https://music.youtube.com/");

    public static string UserDataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YtMusicController",
        "WebView2Profile");

    public static bool IsAllowedUri(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
            return false;

        var host = uri.IdnHost;
        return IsHost(host, "youtube.com") ||
               IsHost(host, "google.com") ||
               IsHost(host, "googleusercontent.com") ||
               IsHost(host, "gstatic.com");
    }

    public static bool IsTrustedMusicOrigin(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.Equals(uri.IdnHost, "music.youtube.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsHost(string host, string suffix) =>
        host.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase);
}
