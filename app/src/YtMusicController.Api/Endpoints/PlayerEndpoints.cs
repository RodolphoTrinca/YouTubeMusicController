using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using YtMusicController.Core.Configuration;
using YtMusicController.Core.Player;

namespace YtMusicController.Api.Endpoints;

public static class PlayerEndpoints
{
    public static IEndpointRouteBuilder MapPlayerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var player = endpoints.MapGroup("/api");

        player.MapPost("/player/play", Command((c, _) => c.PlayAsync()));
        player.MapPost("/player/pause", Command((c, _) => c.PauseAsync()));
        player.MapPost("/player/toggle", Command((c, _) => c.TogglePlayPauseAsync()));
        player.MapPost("/player/next", Command((c, _) => c.NextAsync()));
        player.MapPost("/player/previous", Command((c, _) => c.PreviousAsync()));
        player.MapPost("/player/like", Command((c, _) => c.LikeAsync()));
        player.MapPost("/player/unlike", Command((c, _) => c.UnlikeAsync()));
        player.MapPost("/player/toggle-like", Command((c, _) => c.ToggleLikeAsync()));

        player.MapPost("/volume/up", Command((c, s) => c.IncreaseVolumeAsync(s.Current.VolumeStep)));
        player.MapPost("/volume/down", Command((c, s) => c.DecreaseVolumeAsync(s.Current.VolumeStep)));
        player.MapPut("/volume/{percentage:int}", async (int percentage, IYouTubeMusicController controller) =>
        {
            if (percentage is < 0 or > 100)
                return Results.Problem(statusCode: 400, title: "Invalid volume", detail: "Volume must be between 0 and 100.");

            await controller.SetVolumeAsync(percentage);
            return Results.Ok(new { accepted = true, volume = percentage });
        });

        player.MapPost("/volume/mute", Command((c, _) => c.MuteAsync()));
        player.MapPost("/volume/unmute", Command((c, _) => c.UnmuteAsync()));
        player.MapPost("/volume/toggle-mute", Command((c, _) => c.ToggleMuteAsync()));
        player.MapGet("/player/status", async (IYouTubeMusicController controller) =>
            Results.Ok(await controller.GetStateAsync()));
        player.MapGet("/player/events", PlayerWebSocketEndpoint.StreamAsync);

        return endpoints;
    }

    private static Func<IYouTubeMusicController, IAppSettingsStore, Task<IResult>> Command(
        Func<IYouTubeMusicController, IAppSettingsStore, Task> command) =>
        async (controller, settings) =>
        {
            await command(controller, settings);
            return Results.Ok(new { accepted = true });
        };
}
