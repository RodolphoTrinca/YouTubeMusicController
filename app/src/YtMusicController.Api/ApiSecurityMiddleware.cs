using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using YtMusicController.Core.Configuration;
using YtMusicController.Core.Player;

namespace YtMusicController.Api;

public sealed class ApiSecurityMiddleware(RequestDelegate next, ILogger<ApiSecurityMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IAppSettingsStore settings)
    {
        var remote = context.Connection.RemoteIpAddress;
        var stopwatch = Stopwatch.StartNew();
        logger.LogDebug(
            "Local API request {TraceIdentifier} received: {Method} {Path}; remote {RemoteAddress}; content length {ContentLength}",
            context.TraceIdentifier, context.Request.Method, context.Request.Path,
            remote?.ToString() ?? "unknown", context.Request.ContentLength);

        if (remote is not null && !IPAddress.IsLoopback(remote))
        {
            logger.LogWarning("Rejected non-loopback API request {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteProblem(context, 403, "Forbidden", "The API accepts loopback requests only.");
            return;
        }

        var header = context.Request.Headers.Authorization.ToString();
        var supplied = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : null;
        if (string.IsNullOrEmpty(supplied) &&
            context.WebSockets.IsWebSocketRequest &&
            context.Request.Path.Equals("/api/player/events", StringComparison.OrdinalIgnoreCase))
        {
            supplied = WebSocketTokenProtocol.ReadToken(context);
        }

        if (!TokenAuthenticator.Validate(supplied, settings.GetApiToken()))
        {
            logger.LogWarning("Rejected API request with invalid authentication: {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteProblem(context, 401, "Unauthorized", "A valid bearer token is required.");
            return;
        }

        try
        {
            await next(context);
        }
        catch (PlayerNotReadyException ex)
        {
            logger.LogWarning(
                "Local API request {TraceIdentifier} cannot run {Method} {Path}: {Reason}",
                context.TraceIdentifier, context.Request.Method, context.Request.Path, ex.Message);
            await WriteProblem(context, 409, "Player not ready", ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            await WriteProblem(context, 400, "Invalid request", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled local API error for {Path}", context.Request.Path);
            await WriteProblem(context, 500, "Internal server error", "The command could not be completed.");
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "Local API {Method} {Path} returned {StatusCode} in {ElapsedMilliseconds} ms; trace {TraceIdentifier}",
                context.Request.Method, context.Request.Path, context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds, context.TraceIdentifier);
        }
    }

    private static Task WriteProblem(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        return Results.Problem(statusCode: status, title: title, detail: detail).ExecuteAsync(context);
    }
}
