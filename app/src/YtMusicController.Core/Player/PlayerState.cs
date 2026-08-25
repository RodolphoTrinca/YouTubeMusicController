namespace YtMusicController.Core.Player;

public sealed record PlayerState(
    bool IsPlaying,
    bool IsMuted,
    int Volume,
    bool? IsLiked,
    TrackInfo? Track)
{
    public static PlayerState Empty { get; } = new(false, false, 100, null, null);
}
