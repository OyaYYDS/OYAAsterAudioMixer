// 托盘图标（WinForms NotifyIcon，零外部依赖）
// 图标文件：exe 同目录 icon.ico，不存在则回退系统图标
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AsterAudioRouter.Gui;

public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon? _iconResource;

    public TrayIcon(Action openMixer, Action openSettings, Action exit)
    {
        var iconFile = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        try
        {
            if (File.Exists(iconFile))
                _iconResource = new Icon(iconFile);
        }
        catch
        {
            _iconResource = null;
        }

        var menu = new ContextMenuStrip();
        menu.Items.Add("打开合成器", null, (_, _) => openMixer());
        menu.Items.Add("设置", null, (_, _) => openSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());

        _notifyIcon = new NotifyIcon
        {
            Text = "OYA ASTER 音量合成器",
            Icon = _iconResource ?? SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => openMixer();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _iconResource?.Dispose();
    }
}
