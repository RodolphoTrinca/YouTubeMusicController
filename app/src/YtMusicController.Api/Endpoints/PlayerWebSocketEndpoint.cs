using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using YtMusicController.Core.Player;
using YtMusicController.Api;

namespace YtMusicController.Api.Endpoints;

internal static class PlayerWebSocketEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task StreamAsync(
        HttpContext context,
        IYouTubeMusicController controller,
        ILoggerFactory loggerFactory)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "WebSocket required",
                detail = "Connect to this endpoint using a WebSocket upgrade request."
            });
            return;
        }

        var logger = loggerFactory.CreateLogger("YtMusicController.Api.PlayerWebSocket");
        var selectedProtocol = WebSocketTokenProtocol.GetSelectedProtocol(context);
        using var socket = await context.WebSockets.AcceptWebSocketAsync(selectedProtocol);
        using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            context.RequestAborted);
        var states = Channel.CreateBounded<PlayerState>(new BoundedChannelOptions(4)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        void OnStateChanged(object? _, PlayerState state) => states.Writer.TryWrite(state);
        controller.StateChanged += OnStateChanged;
        states.Writer.TryWrite(await controller.GetStateAsync());
        logger.LogInformation("Companion player-state WebSocket connected");

        try
        {
            var sendTask = SendStatesAsync(socket, states.Reader, connectionCancellation.Token);
            var receiveTask = ReceiveUntilClosedAsync(socket, connectionCancellation.Token);
            await Task.WhenAny(sendTask, receiveTask);
            connectionCancellation.Cancel();
            await IgnoreCancellationAsync(sendTask);
            await IgnoreCancellationAsync(receiveTask);
        }
        finally
        {
            controller.StateChanged -= OnStateChanged;
            states.Writer.TryComplete();
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None);
                }
                catch (WebSocketException)
                {
                }
            }
            logger.LogInformation("Companion player-state WebSocket disconnected");
        }
    }

    private static async Task SendStatesAsync(
        WebSocket socket,
        ChannelReader<PlayerState> states,
        CancellationToken cancellationToken)
    {
        await foreach (var state in states.ReadAllAsync(cancellationToken))
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions);
            await socket.SendAsync(
                payload,
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);
        }
    }

    private static async Task ReceiveUntilClosedAsync(
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[256];
        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
                return;
        }
    }

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
    }
}
