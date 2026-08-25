namespace YtMusicController.Core.Configuration;

public interface IAppSettingsStore
{
    AppSettings Current { get; }
    string GetApiToken();
    AppSettings Update(
        int port,
        int volumeStep,
        VolumeControlMethod volumeControlMethod,
        bool minimizeToTray,
        bool showDebugLogWindow);
    string RegenerateApiToken();
}
