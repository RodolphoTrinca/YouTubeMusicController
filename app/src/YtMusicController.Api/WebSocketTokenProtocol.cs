using System.Text;
using Microsoft.AspNetCore.Http;

namespace YtMusicController.Api;

internal static class WebSocketTokenProtocol
{
    private const string Prefix = "ytmusic-controller.auth.";
    private const string ContextKey = "YtMusicController.WebSocketProtocol";

    public static string? ReadToken(HttpContext context)
    {
        foreach (var value in context.Request.Headers.SecWebSocketProtocol)
        {
            if (string.IsNullOrEmpty(value))
                continue;
            foreach (var candidateValue in value.Split(','))
            {
                var candidate = candidateValue.Trim();
                if (!candidate.StartsWith(Prefix, StringComparison.Ordinal))
                    continue;

                try
                {
                    var encoded = candidate[Prefix.Length..]
                        .Replace('-', '+')
                        .Replace('_', '/');
                    encoded = encoded.PadRight(encoded.Length + (4 - encoded.Length % 4) % 4, '=');
                    var token = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                    context.Items[ContextKey] = candidate;
                    return token;
                }
                catch (FormatException)
                {
                    return null;
                }
            }
        }

        return null;
    }

    public static string? GetSelectedProtocol(HttpContext context) =>
        context.Items.TryGetValue(ContextKey, out var value) ? value as string : null;
}
