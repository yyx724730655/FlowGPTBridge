namespace FlowGPTBridge.Models;

/// <summary>
/// 一个 Flow 结果项最终对应的完整执行计划。
/// </summary>
/// <param name="TargetMode">准备切换到的目标模式。</param>
/// <param name="SwitchMode">是否发送模式快捷键。</param>
/// <param name="CreateNewChat">是否发送新建聊天快捷键。</param>
/// <param name="Prompt">成功新建聊天后写入剪贴板的 Prompt。</param>
public sealed record ExecutionPlan(
    TargetMode TargetMode,
    bool SwitchMode,
    bool CreateNewChat,
    string? Prompt);
