namespace FlowGPTBridge.Models;

/// <summary>
/// 与 Flow SDK 无关的结果描述，便于独立测试列表生成逻辑。
/// </summary>
public sealed record ResultDescriptor(
    string Title,
    string SubTitle,
    ExecutionPlan Plan);
