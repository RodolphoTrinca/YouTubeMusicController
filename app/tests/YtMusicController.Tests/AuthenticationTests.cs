using YtMusicController.Core.Configuration;

namespace YtMusicController.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public void Generated_token_is_strong_and_validates()
    {
        var token = TokenAuthenticator.GenerateToken();
        Assert.True(Convert.FromBase64String(token).Length >= 32);
        Assert.True(TokenAuthenticator.Validate(token, token));
        Assert.False(TokenAuthenticator.Validate(token + "x", token));
        Assert.False(TokenAuthenticator.Validate(null, token));
    }

    [Fact]
    public void Settings_round_trip_and_token_regeneration_work()
    {
        var path = Path.Combine(Path.GetTempPath(), "YtMusicControllerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new AppSettingsStore(path);
            var original = store.GetApiToken();
            store.Update(
                49123, 10, VolumeControlMethod.KeyboardShortcuts, false, true);
            var regenerated = store.RegenerateApiToken();

            var reloaded = new AppSettingsStore(path);
            Assert.Equal(49123, reloaded.Current.Port);
            Assert.Equal(10, reloaded.Current.VolumeStep);
            Assert.Equal(
                VolumeControlMethod.KeyboardShortcuts,
                reloaded.Current.VolumeControlMethod);
            Assert.False(reloaded.Current.MinimizeToTray);
            Assert.True(reloaded.Current.ShowDebugLogWindow);
            Assert.Equal(regenerated, reloaded.GetApiToken());
            Assert.NotEqual(original, regenerated);
        }
        finally
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
    }
}
