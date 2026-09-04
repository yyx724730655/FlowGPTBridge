namespace FlowGPTBridge.Models;

/// <summary>
/// 查询的语义类别。执行层只消费解析后的语义，不再读取原始查询文本。
/// </summary>
public enum QueryKind
{
    OpenCurrent,
    Prompt,
    ExplicitMode,
    NewCurrent
}
