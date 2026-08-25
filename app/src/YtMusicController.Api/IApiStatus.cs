namespace YtMusicController.Api;

public interface IApiStatus
{
    bool IsRunning { get; }
    int Port { get; }
    string? BaseUrl { get; }
    bool UsedFallbackPort { get; }
    string? ErrorMessage { get; }
    event EventHandler? StatusChanged;
}
