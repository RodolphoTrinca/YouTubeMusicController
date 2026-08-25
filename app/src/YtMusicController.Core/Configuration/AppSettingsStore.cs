using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace YtMusicController.Core.Configuration;

public sealed class AppSettingsStore : IAppSettingsStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("YtMusicController.ApiToken.v1");
    private readonly object _gate = new();
    private readonly string _settingsPath;
    private AppSettings _current;

    public AppSettingsStore(string? baseDirectory = null)
    {
        var directory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YtMusicController");
        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
        _current = LoadOrCreate();
    }

    public AppSettings Current { get { lock (_gate) return _current; } }

    public string GetApiToken()
    {
        lock (_gate)
        {
            try
            {
                var protectedBytes = Convert.FromBase64String(_current.ProtectedApiToken);
                var plain = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                return RegenerateApiToken();
            }
        }
    }

    public AppSettings Update(
        int port,
        int volumeStep,
        VolumeControlMethod volumeControlMethod,
        bool minimizeToTray,
        bool showDebugLogWindow)
    {
        Validate(port, volumeStep, volumeControlMethod);
        lock (_gate)
        {
            _current = _current with
            {
                Port = port,
                VolumeStep = volumeStep,
                VolumeControlMethod = volumeControlMethod,
                MinimizeToTray = minimizeToTray,
                ShowDebugLogWindow = showDebugLogWindow
            };
            Save();
            return _current;
        }
    }

    public string RegenerateApiToken()
    {
        lock (_gate)
        {
            var token = TokenAuthenticator.GenerateToken();
            _current = _current with { ProtectedApiToken = Protect(token) };
            Save();
            return token;
        }
    }

    private AppSettings LoadOrCreate()
    {
        AppSettings settings;
        try
        {
            settings = File.Exists(_settingsPath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath)) ?? new AppSettings()
                : new AppSettings();
            Validate(settings.Port, settings.VolumeStep, settings.VolumeControlMethod);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentOutOfRangeException)
        {
            settings = new AppSettings();
        }

        _current = settings;
        if (string.IsNullOrWhiteSpace(_current.ProtectedApiToken))
            _current = _current with { ProtectedApiToken = Protect(TokenAuthenticator.GenerateToken()) };
        Save();
        return _current;
    }

    private string Protect(string token)
    {
        var plain = Encoding.UTF8.GetBytes(token);
        return Convert.ToBase64String(ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser));
    }

    private void Save()
    {
        var temporary = _settingsPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_current, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, _settingsPath, true);
    }

    private static void Validate(int port, int step, VolumeControlMethod volumeControlMethod)
    {
        if (port is < 1024 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1024 and 65535.");
        if (step is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(step), "Volume step must be between 1 and 100.");
        if (!Enum.IsDefined(volumeControlMethod))
            throw new ArgumentOutOfRangeException(
                nameof(volumeControlMethod), "Select a supported volume control method.");
    }
}
