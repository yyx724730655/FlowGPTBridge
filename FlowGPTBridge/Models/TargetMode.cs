namespace FlowGPTBridge.Models;

/// <summary>
/// ChatGPT 中可由插件选择的目标模式。
/// Current 表示保持用户当前所在的模式。
/// </summary>
public enum TargetMode
{
    Current,
    Chat,
    Work,
    Codex
}
