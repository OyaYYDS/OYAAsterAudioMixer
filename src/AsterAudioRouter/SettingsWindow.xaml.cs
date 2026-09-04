// 设置窗口：路由规则编辑（写 config.json，agent 热重载）、手动覆盖管理、本座位自启开关
using AsterAudioRouter.Gui;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AsterAudioRouter;

public partial class SettingsWindow : Window
{
    private readonly OverridesStore _overrides;
    private readonly string _configPath;
    private readonly ObservableCollection<RouterEntry> _rules = new();
    private readonly ObservableCollection<OverrideRecord> _overrideRows = new();

    public ObservableCollection<string> PlaybackDevices { get; } = new();
    public ObservableCollection<string> CaptureDevices { get; } = new();

    public SettingsWindow(OverridesStore overrides)
    {
        InitializeComponent();
        _overrides = overrides;
        _configPath = RouterConfig.FindConfigPath(AppContext.BaseDirectory);
        DataContext = this;
        ConfigPathText.Text = "配置文件: " + _configPath;
        RulesList.ItemsSource = _rules;
        OverridesList.ItemsSource = _overrideRows;
        Loaded += async (_, _) => await LoadAllAsync();
        _overrides.Changed += RefreshOverrides;
    }

    private async Task LoadAllAsync()
    {
        try
        {
            var devices = await AudioDevices.ListAsync();
            PlaybackDevices.Clear();
            foreach (var d in devices.Where(d => d.IsRender))
                PlaybackDevices.Add(d.Name);
            CaptureDevices.Clear();
            foreach (var d in devices.Where(d => !d.IsRender))
                CaptureDevices.Add(d.Name);
        }
        catch (Exception ex)
        {
            AgentLogger.Log($"设置窗口设备列表加载失败: {ex.Message}");
        }
        ReloadRules();
        RefreshOverrides();
        AutostartCheck.IsChecked = Autostart.IsInstalled();
    }

    private void ReloadRules()
    {
        _rules.Clear();
        try
        {
            foreach (var entry in RouterConfig.Load(_configPath))
                _rules.Add(entry);
        }
        catch (Exception ex)
        {
            AgentLogger.Log($"规则读取失败: {ex.Message}");
        }
    }

    private void RefreshOverrides()
    {
        _overrideRows.Clear();
        foreach (var record in _overrides.All)
            _overrideRows.Add(record);
    }

    // ---------- 路由规则 ----------

    private void AddRuleButton_Click(object sender, RoutedEventArgs e)
        => _rules.Add(new RouterEntry());

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is RouterEntry entry)
            _rules.Remove(entry);
    }

    private void SaveRulesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            File.WriteAllText(_configPath, JsonSerializer.Serialize(_rules.ToList(), RouterConfig.SaveOptions));
            AgentLogger.Log("规则已保存（agent 将自动热重载）");
            System.Windows.MessageBox.Show("规则已保存，agent 已自动重载。", "保存成功");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("保存失败: " + ex.Message, "保存失败");
        }
    }

    // ---------- 手动覆盖 ----------

    private void DeleteOverride_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is OverrideRecord record)
        {
            _overrides.Remove(record.Exe);
            AgentLogger.Log($"已删除手动覆盖: {record.Exe}（agent 恢复接管）");
        }
    }

    // ---------- 自启 ----------

    private void Autostart_Changed(object sender, RoutedEventArgs e)
    {
        if (AutostartCheck.IsChecked != null)
        {
            if (AutostartCheck.IsChecked == true)
                Autostart.Install();
            else
                Autostart.Uninstall();
        }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        var logDir = Path.Combine(AppContext.BaseDirectory, "p1logs");
        try
        {
            Process.Start("explorer.exe", logDir);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("打开日志失败: " + ex.Message);
        }
    }
}
