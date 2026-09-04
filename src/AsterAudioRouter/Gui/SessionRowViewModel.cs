// 合成器列表的一行：一个正在出声的应用
using AsterAudioRouter.Interop.MMDeviceAPI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;

namespace AsterAudioRouter.Gui;

public sealed class SessionRowViewModel : INotifyPropertyChanged
{
    public uint Pid { get; }
    public string ExeName { get; }

    private string _displayName = "";
    public string DisplayName
    {
        get => _displayName;
        set { if (_displayName != value) { _displayName = value; Raise(nameof(DisplayName)); } }
    }

    private ImageSource? _icon;
    public ImageSource? Icon
    {
        get => _icon;
        set { if (_icon != value) { _icon = value; Raise(nameof(Icon)); } }
    }

    private double _volume = 1.0;
    public double Volume
    {
        get => _volume;
        set { if (Math.Abs(_volume - value) > 0.001) { _volume = value; Raise(nameof(Volume)); } }
    }

    private bool _muted;
    public bool Muted
    {
        get => _muted;
        set { if (_muted != value) { _muted = value; Raise(nameof(Muted)); } }
    }

    private string _deviceSelection = "";
    public string DeviceSelection
    {
        get => _deviceSelection;
        set { if (_deviceSelection != value) { _deviceSelection = value; Raise(nameof(DeviceSelection)); } }
    }

    private string _captureSelection = "";
    public string CaptureSelection
    {
        get => _captureSelection;
        set { if (_captureSelection != value) { _captureSelection = value; Raise(nameof(CaptureSelection)); } }
    }

    /// <summary>共享的设备下拉项（由 MixerController 注入同一实例）。</summary>
    public ObservableCollection<string> DeviceChoices { get; set; } = new();
    public ObservableCollection<string> CaptureChoices { get; set; } = new();

    /// <summary>用户正在拖动滑条（轮询回读暂停覆盖）。</summary>
    public bool UserDragging { get; set; }

    /// <summary>程序正在改下拉选中项（防 SelectionChanged 循环）。</summary>
    public bool UpdatingDeviceSelection { get; set; }

    /// <summary>每轮扫描更新的音量控制对象（同一 pid 可能有多个会话，全部保留）。</summary>
    public List<ISimpleAudioVolume> VolumeControls { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public SessionRowViewModel(uint pid, string exeName)
    {
        Pid = pid;
        ExeName = exeName;
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void ApplyVolume()
    {
        // 对该 pid 的全部会话应用（Windows 每应用音量 = 作用其所有会话）
        foreach (var volume in VolumeControls)
        {
            try
            {
                Guid empty = Guid.Empty;
                volume.SetMasterVolume((float)Volume, ref empty);
            }
            catch (Exception ex)
            {
                AgentLogger.Log($"设置音量失败 pid={Pid} ({ExeName}): {ex.Message}");
            }
        }
        float readBack = -1;
        if (VolumeControls.Count > 0)
        {
            try { VolumeControls[0].GetMasterVolume(out readBack); } catch { }
        }
        AgentLogger.Log($"音量操作 pid={Pid} ({ExeName}) 会话数={VolumeControls.Count} 设置={Volume:F2} 回读={readBack:F2}");
    }

    public void ApplyMute(bool muted)
    {
        foreach (var volume in VolumeControls)
        {
            try
            {
                Guid empty = Guid.Empty;
                volume.SetMute(muted ? 1 : 0, ref empty);
            }
            catch (Exception ex)
            {
                AgentLogger.Log($"设置静音失败 pid={Pid} ({ExeName}): {ex.Message}");
            }
        }
        AgentLogger.Log($"静音操作 pid={Pid} ({ExeName}) 会话数={VolumeControls.Count} 静音={muted}");
    }
}
