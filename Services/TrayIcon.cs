using GameLauncher.Localization;
using Forms = System.Windows.Forms;

namespace GameLauncher.Services;

/// <summary>
/// Ícone na área de notificação (ao lado do relógio), usado quando a app fica a correr escondida
/// para continuar a contar o tempo de jogo.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Action _open;
    private readonly Action _exit;

    public TrayIcon(Action open, Action exit)
    {
        _open = open;
        _exit = exit;
        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "MyGameLauncher",
            Visible = false,
        };
        _icon.DoubleClick += (_, _) => _open();
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) _open(); };
        BuildMenu();
        Loc.Changed += BuildMenu;
    }

    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } exe && System.Drawing.Icon.ExtractAssociatedIcon(exe) is { } icon) return icon;
        }
        catch { }
        return System.Drawing.SystemIcons.Application;
    }

    private void BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(Loc.T("Tray_Open"), null, (_, _) => _open());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Loc.T("Tray_Exit"), null, (_, _) => _exit());
        var old = _icon.ContextMenuStrip;
        _icon.ContextMenuStrip = menu;
        old?.Dispose();
    }

    public bool Visible
    {
        get => _icon.Visible;
        set => _icon.Visible = value;
    }

    /// <summary>Pequena mensagem junto ao relógio (ex.: a primeira vez que a app fica escondida).</summary>
    public void Notify(string text)
    {
        try { _icon.ShowBalloonTip(4000, "MyGameLauncher", text, Forms.ToolTipIcon.None); } catch { }
    }

    public void Dispose()
    {
        Loc.Changed -= BuildMenu;
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }
}
