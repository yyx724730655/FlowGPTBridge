using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlowGPTBridge.Models;
using FlowGPTBridge.Services;

namespace FlowGPTBridge.Settings;

/// <summary>
/// 插件设置面板。录制期间只观察按键，不注册全局热键。
/// </summary>
public partial class SettingsControl : UserControl
{
    private readonly PluginSettings _settings;
    private readonly Action _save;
    private readonly Func<HotkeySetting, Task<ExecutionResult>> _testShortcut;
    private string? _recordingTarget;
    private bool _initializing = true;

    public SettingsControl(
        PluginSettings settings,
        Action save,
        Func<HotkeySetting, Task<ExecutionResult>> testShortcut)
    {
        _settings = settings;
        _save = save;
        _testShortcut = testShortcut;

        InitializeComponent();
        Focusable = true;
        PreviewKeyDown += SettingsControl_PreviewKeyDown;

        DefaultModeCombo.ItemsSource = new[]
        {
            TargetMode.Chat,
            TargetMode.Work,
            TargetMode.Codex,
            TargetMode.Current
        };

        RefreshAll();
        _initializing = false;
    }

    private void RefreshAll()
    {
        ChatHotkeyText.Text = HotkeyFormatter.Format(_settings.ChatShortcut);
        WorkHotkeyText.Text = HotkeyFormatter.Format(_settings.WorkShortcut);
        CodexHotkeyText.Text = HotkeyFormatter.Format(_settings.CodexShortcut);
        NewChatHotkeyText.Text = HotkeyFormatter.Format(_settings.NewChatShortcut);
        DefaultModeCombo.SelectedItem = _settings.DefaultPromptMode;
        LaunchTimeoutText.Text = _settings.LaunchTimeoutMs.ToString();
        DebugLoggingCheck.IsChecked = _settings.DebugLogging;
        ExecutablePathText.Text = _settings.ChatGptExecutablePath ?? string.Empty;
        AppUserModelIdText.Text = _settings.ChatGptAppUserModelId ?? string.Empty;
    }

    private void RecordHotkey_Click(object sender, RoutedEventArgs e)
    {
        _recordingTarget = (sender as FrameworkElement)?.Tag as string;
        RecordingHint.Text = _recordingTarget is null
            ? string.Empty
            : "请按下快捷键。Esc 取消，Backspace 清除；不能只录制修饰键。";
        Focus();
        Keyboard.Focus(this);
    }

    private void SettingsControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_recordingTarget is null)
        {
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            StopRecording("已取消录制。");
            return;
        }

        if (key == Key.Back)
        {
            SetHotkey(_recordingTarget, new HotkeySetting());
            StopRecording("快捷键已清除。");
            return;
        }

        if (HotkeyFormatter.IsModifierKey(key))
        {
            RecordingHint.Text = "请继续按下一个普通键；不能只录制修饰键。";
            return;
        }

        var candidate = new HotkeySetting
        {
            Modifiers = ReadModifiers(),
            Key = key.ToString()
        };

        var conflict = FindConflict(_recordingTarget, candidate);
        if (conflict is not null)
        {
            RecordingHint.Text = $"快捷键与“{conflict}”冲突，请录制其他组合。";
            return;
        }

        SetHotkey(_recordingTarget, candidate);
        _save();
        RefreshAll();
        StopRecording($"已保存：{HotkeyFormatter.Format(candidate)}");
    }

    private void ClearHotkey_Click(object sender, RoutedEventArgs e)
    {
        var target = (sender as FrameworkElement)?.Tag as string;
        if (target is null)
        {
            return;
        }

        SetHotkey(target, new HotkeySetting());
        _save();
        RefreshAll();
        RecordingHint.Text = $"{DisplayName(target)} 快捷键已清除。";
    }

    private async void TestHotkey_Click(object sender, RoutedEventArgs e)
    {
        var target = (sender as FrameworkElement)?.Tag as string;
        if (target is null)
        {
            return;
        }

        var result = await _testShortcut(GetHotkey(target));
        MessageBox.Show(
            result.Message,
            result.Title,
            MessageBoxButton.OK,
            result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void ResetHotkey_Click(object sender, RoutedEventArgs e)
    {
        var target = (sender as FrameworkElement)?.Tag as string;
        if (target is null)
        {
            return;
        }

        var defaultHotkey = target switch
        {
            "Chat" => HotkeySetting.Alt("D1"),
            "Work" => HotkeySetting.Alt("D2"),
            "Codex" => HotkeySetting.Alt("D3"),
            "NewChat" => HotkeySetting.Ctrl("N"),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "未知快捷键设置")
        };

        var conflict = FindConflict(target, defaultHotkey);
        if (conflict is not null)
        {
            RecordingHint.Text = $"默认快捷键与“{conflict}”冲突，请先修改冲突项。";
            return;
        }

        SetHotkey(target, defaultHotkey);
        _save();
        RefreshAll();
        RecordingHint.Text = $"{DisplayName(target)} 已恢复默认值。";
    }

    private void DefaultModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || DefaultModeCombo.SelectedItem is not TargetMode mode)
        {
            return;
        }

        _settings.DefaultPromptMode = mode;
        _save();
    }

    private void LaunchTimeoutText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(LaunchTimeoutText.Text, out var timeout))
        {
            _settings.LaunchTimeoutMs = Math.Clamp(timeout, 1000, 60000);
            _save();
        }

        LaunchTimeoutText.Text = _settings.LaunchTimeoutMs.ToString();
    }

    private void DebugLoggingCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        _settings.DebugLogging = DebugLoggingCheck.IsChecked == true;
        _save();
    }

    private void AdvancedSetting_LostFocus(object sender, RoutedEventArgs e)
    {
        _settings.ChatGptExecutablePath = NullIfWhiteSpace(ExecutablePathText.Text);
        _settings.ChatGptAppUserModelId = NullIfWhiteSpace(AppUserModelIdText.Text);
        _save();
    }

    private HotkeySetting GetHotkey(string target) => target switch
    {
        "Chat" => _settings.ChatShortcut,
        "Work" => _settings.WorkShortcut,
        "Codex" => _settings.CodexShortcut,
        "NewChat" => _settings.NewChatShortcut,
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "未知快捷键设置")
    };

    private void SetHotkey(string target, HotkeySetting hotkey)
    {
        switch (target)
        {
            case "Chat":
                _settings.ChatShortcut = hotkey;
                break;
            case "Work":
                _settings.WorkShortcut = hotkey;
                break;
            case "Codex":
                _settings.CodexShortcut = hotkey;
                break;
            case "NewChat":
                _settings.NewChatShortcut = hotkey;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, "未知快捷键设置");
        }
    }

    private string? FindConflict(string target, HotkeySetting candidate)
    {
        return new[] { "Chat", "Work", "Codex", "NewChat" }
            .Where(name => !string.Equals(name, target, StringComparison.Ordinal))
            .FirstOrDefault(name => candidate.Equals(GetHotkey(name))) is { } conflict
            ? DisplayName(conflict)
            : null;
    }

    private static List<string> ReadModifiers()
    {
        var modifiers = Keyboard.Modifiers;
        var result = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) result.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) result.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) result.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) result.Add("Win");
        return result;
    }

    private void StopRecording(string message)
    {
        _recordingTarget = null;
        RecordingHint.Text = message;
    }

    private static string DisplayName(string target) => target == "NewChat" ? "新聊天" : target;

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
