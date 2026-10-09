namespace YtMusicController.Core.Configuration;

public enum VolumeControlMethod
{
    PlayerApi,
    KeyboardShortcuts
}

public sealed record AppSettings
{
    public const int DefaultPort = 38472;
    public const int DefaultVolumeStep = 5;

    public int Port { get; init; } = DefaultPort;
    public int VolumeStep { get; init; } = DefaultVolumeStep;
    public VolumeControlMethod VolumeControlMethod { get; init; } = VolumeControlMethod.PlayerApi;
    public bool MinimizeToTray { get; init; }
    public bool ShowDebugLogWindow { get; init; }
    public string ProtectedApiToken { get; init; } = string.Empty;
}
