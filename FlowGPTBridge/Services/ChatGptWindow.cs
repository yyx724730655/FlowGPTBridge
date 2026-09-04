namespace FlowGPTBridge.Services;

/// <summary>
/// 已确认属于 ChatGPT 进程的顶层窗口。
/// </summary>
public sealed record ChatGptWindow(nint Handle, uint ProcessId);
