namespace FlowGPTBridge.Models;

/// <summary>
/// Flow 自动持久化的插件设置。默认值与开发说明保持一致。
/// </summary>
public sealed class PluginSettings
{
    public string ActionKeyword { get; set; } = "gpt";

    public TargetMode DefaultPromptMode { get; set; } = TargetMode.Chat;

    public HotkeySetting ChatShortcut { get; set; } = HotkeySetting.Alt("D1");

    public HotkeySetting WorkShortcut { get; set; } = HotkeySetting.Alt("D2");

    public HotkeySetting CodexShortcut { get; set; } = HotkeySetting.Alt("D3");

    public HotkeySetting NewChatShortcut { get; set; } = HotkeySetting.Ctrl("N");

    public int LaunchTimeoutMs { get; set; } = 8000;

    public bool DebugLogging { get; set; }

    /// <summary>
    /// 可选手动覆盖项，用于无法从 Windows 开始菜单注册信息定位应用的情况。
    /// </summary>
    public string? ChatGptExecutablePath { get; set; }

    public string? ChatGptAppUserModelId { get; set; }

    public HotkeySetting? GetModeShortcut(TargetMode mode) => mode switch
    {
        TargetMode.Chat => ChatShortcut,
        TargetMode.Work => WorkShortcut,
        TargetMode.Codex => CodexShortcut,
        TargetMode.Current => null,
        _ => null
    };

    public IReadOnlyList<(string Name, HotkeySetting Hotkey)> GetNamedHotkeys() =>
    [
        ("Chat", ChatShortcut),
        ("Work", WorkShortcut),
        ("Codex", CodexShortcut),
        ("新聊天", NewChatShortcut)
    ];

    public IReadOnlyList<string> FindHotkeyConflicts()
    {
        return GetNamedHotkeys()
            .Where(item => !item.Hotkey.IsEmpty)
            .GroupBy(item => item.Hotkey.ToCanonicalString(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join("、", group.Select(item => item.Name)))
            .ToList();
    }
}
