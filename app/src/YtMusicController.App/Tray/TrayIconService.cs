using System.Drawing;
using System.Windows.Forms;

namespace YtMusicController.App.Tray;

public sealed class TrayIconService : IDisposable
{
    private NotifyIcon? _icon;
    private Icon? _appIcon;

    public void Initialize(Action show, Action settings, Action quit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show YouTube Music", null, (_, _) => show());
        menu.Items.Add("Settings", null, (_, _) => settings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => quit());

        _appIcon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
        _icon = new NotifyIcon
        {
            Icon = _appIcon ?? SystemIcons.Application,
            Text = "YouTube Music Controller",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => show();
    }

    public void Dispose()
    {
        if (_icon is null)
            return;
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _icon = null;
        _appIcon?.Dispose();
        _appIcon = null;
    }
}
