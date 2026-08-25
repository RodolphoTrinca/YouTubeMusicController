namespace YtMusicController.App.Diagnostics;

public sealed class DebugLogWindowManager(DebugLogStore store)
{
    private DebugLogWindow? _window;

    public void Show()
    {
        if (_window is null)
        {
            _window = new DebugLogWindow(store);
            _window.Closed += (_, _) => _window = null;
            _window.Show();
        }
        else if (!_window.IsVisible)
        {
            _window.Show();
        }

        _window.WindowState = System.Windows.WindowState.Normal;
        _window.Activate();
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
            Show();
        else
            Close();
    }

    public void Close()
    {
        _window?.Close();
        _window = null;
    }
}
