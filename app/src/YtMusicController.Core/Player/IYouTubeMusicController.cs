namespace YtMusicController.Core.Player;

public interface IYouTubeMusicController
{
    event EventHandler<PlayerState>? StateChanged;

    Task PlayAsync();
    Task PauseAsync();
    Task TogglePlayPauseAsync();
    Task NextAsync();
    Task PreviousAsync();
    Task SetVolumeAsync(int percentage);
    Task IncreaseVolumeAsync(int step);
    Task DecreaseVolumeAsync(int step);
    Task MuteAsync();
    Task UnmuteAsync();
    Task ToggleMuteAsync();
    Task LikeAsync();
    Task UnlikeAsync();
    Task ToggleLikeAsync();
    Task<PlayerState> GetStateAsync();
}
