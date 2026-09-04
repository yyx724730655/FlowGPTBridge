namespace FlowGPTBridge.Services;

/// <summary>
/// 执行状态机的终态，用于向用户准确说明已完成到哪一步。
/// </summary>
public sealed record ExecutionResult(bool Success, string Title, string Message)
{
    public static ExecutionResult Ok(string title, string message) => new(true, title, message);

    public static ExecutionResult Fail(string title, string message) => new(false, title, message);
}
