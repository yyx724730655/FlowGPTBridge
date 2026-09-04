namespace FlowGPTBridge.Models;

/// <summary>
/// CommandParser 的纯数据输出。
/// </summary>
/// <param name="ExplicitMode">用户显式指定的模式；普通 Prompt 时为 null。</param>
/// <param name="Prompt">需要复制的原始 Prompt；空白内容会转换为 null。</param>
/// <param name="CreateNewChat">所选结果是否必须新建聊天。</param>
/// <param name="Kind">查询的语义类别。</param>
public sealed record ParsedQuery(
    TargetMode? ExplicitMode,
    string? Prompt,
    bool CreateNewChat,
    QueryKind Kind);
