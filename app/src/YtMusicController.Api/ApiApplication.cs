using Microsoft.AspNetCore.Builder;
using YtMusicController.Api.Endpoints;

namespace YtMusicController.Api;

public static class ApiApplication
{
    public static WebApplication Configure(WebApplication app)
    {
        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(20)
        });
        app.UseMiddleware<ApiSecurityMiddleware>();
        app.MapPlayerEndpoints();
        return app;
    }
}
