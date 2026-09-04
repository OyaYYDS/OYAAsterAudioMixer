using AsterAudioRouter.Gui;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;

namespace AsterAudioRouter;

public partial class MainWindow : Window
{
    private readonly MixerController _controller;
    private readonly OverridesStore _overrides;

    public MainWindow(MixerController controller, OverridesStore overrides)
    {
        InitializeComponent();
        _controller = controller;
        _overrides = overrides;
        SessionList.ItemsSource = controller.Rows;
        controller.Rows.CollectionChanged += (_, _) => UpdateHeader();
        controller.HeaderChanged += UpdateHeader;
        UpdateHeader();
    }

    private void UpdateHeader()
    {
        HeaderText.Text = $"本座位音频 — 用户 {_controller.CurrentUserName}，{_controller.RuleText}";
        EmptyHint.Visibility = _controller.Rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------- 行内交互 ----------

    private void Slider_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is SessionRowViewModel row)
            row.UserDragging = true;
    }

    private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is SessionRowViewModel row)
        {
            row.UserDragging = false;
            row.ApplyVolume();
        }
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox box && box.DataContext is SessionRowViewModel row)
            row.ApplyMute(box.IsChecked == true);
    }

    private void Device_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => HandleDeviceSelection(sender, render: true);

    private void Capture_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => HandleDeviceSelection(sender, render: false);

    private void HandleDeviceSelection(object sender, bool render)
    {
        if (sender is not ComboBox combo || combo.DataContext is not SessionRowViewModel row)
            return;
        if (row.UpdatingDeviceSelection)
            return;
        if (combo.SelectedItem is not string choice)
            return;

        if (choice == MixerController.AutoChoice)
            _controller.SetAuto(row.ExeName, render);
        else
            _ = _controller.ApplyManualAsync(row, choice, render);
    }

    // ---------- 窗口 ----------

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        // 点 X 只是隐藏到托盘；真正退出走托盘菜单
        e.Cancel = true;
        Hide();
    }

    private void LogsButton_Click(object sender, RoutedEventArgs e)
    {
        var logDir = System.IO.Path.Combine(AppContext.BaseDirectory, "p1logs");
        try
        {
            Process.Start("explorer.exe", logDir);
        }
        catch (System.Exception ex)
        {
            System.Windows.MessageBox.Show("打开日志失败: " + ex.Message);
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = new SettingsWindow(_overrides) { Owner = this };
        settings.ShowDialog();
    }

    private void AddBlacklist_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.DataContext is not SessionRowViewModel row)
            return;
        try
        {
            var configPath = RouterConfig.FindConfigPath(AppContext.BaseDirectory);
            var entries = RouterConfig.Load(configPath);
            var userName = Environment.UserName;
            var entry = RouterConfig.Match(entries, userName);
            if (entry == null)
            {
                entry = new RouterEntry { User = userName };
                entries.Add(entry);
            }

            if (entry.Exclude.Contains(row.ExeName, StringComparer.OrdinalIgnoreCase))
            {
                System.Windows.MessageBox.Show($"{row.ExeName} 已在黑名单中", "黑名单");
                return;
            }
            entry.Exclude = entry.Exclude.Append(row.ExeName).ToArray();
            File.WriteAllText(configPath, JsonSerializer.Serialize(entries, RouterConfig.SaveOptions));
            AgentLogger.Log($"已将 {row.ExeName} 加入黑名单");
            System.Windows.MessageBox.Show($"{row.ExeName} 已加入黑名单，agent 将不再路由它", "黑名单");
        }
        catch (System.Exception ex)
        {
            System.Windows.MessageBox.Show("写入配置失败: " + ex.Message, "黑名单");
        }
    }
}
