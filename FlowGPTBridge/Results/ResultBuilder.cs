using FlowGPTBridge.Models;

namespace FlowGPTBridge.Results;

/// <summary>
/// 根据解析结果和当前设置生成有序结果列表，不执行任何 Windows 操作。
/// </summary>
public sealed class ResultBuilder
{
    private const int PromptPreviewLimit = 72;

    public IReadOnlyList<ResultDescriptor> Build(ParsedQuery query, PluginSettings settings)
    {
        return query.Kind switch
        {
            QueryKind.OpenCurrent => BuildOpenResults(),
            QueryKind.Prompt => BuildPromptResults(query.Prompt!, settings),
            QueryKind.ExplicitMode => BuildExplicitModeResult(query),
            QueryKind.NewCurrent => BuildNewCurrentResult(query),
            _ => throw new ArgumentOutOfRangeException(nameof(query), query.Kind, "未知查询类型")
        };
    }

    private static IReadOnlyList<ResultDescriptor> BuildOpenResults() =>
    [
        new(
            "打开 ChatGPT（保持当前模式）",
            "不切换模式，不新建聊天",
            new ExecutionPlan(TargetMode.Current, false, false, null)),
        CreateOpenModeResult(TargetMode.Chat),
        CreateOpenModeResult(TargetMode.Work),
        CreateOpenModeResult(TargetMode.Codex)
    ];

    private static ResultDescriptor CreateOpenModeResult(TargetMode mode) => new(
        $"打开 ChatGPT — {ModeName(mode)} 模式",
        $"切换到 {ModeName(mode)} 模式，不新建聊天",
        new ExecutionPlan(mode, true, false, null));

    private static IReadOnlyList<ResultDescriptor> BuildPromptResults(string prompt, PluginSettings settings)
    {
        var modes = new List<TargetMode>();
        AddUnique(modes, settings.DefaultPromptMode);
        AddUnique(modes, TargetMode.Chat);
        AddUnique(modes, TargetMode.Work);
        AddUnique(modes, TargetMode.Codex);

        return modes
            .Select(mode => CreatePromptResult(mode, prompt))
            .ToList();
    }

    private static IReadOnlyList<ResultDescriptor> BuildExplicitModeResult(
        ParsedQuery query)
    {
        var mode = query.ExplicitMode ?? TargetMode.Current;
        if (query.Prompt is not null)
        {
            return [CreatePromptResult(mode, query.Prompt)];
        }

        if (mode == TargetMode.Current)
        {
            return
            [
                new ResultDescriptor(
                    "打开 ChatGPT（保持当前模式）",
                    "不切换模式，不新建聊天",
                    new ExecutionPlan(TargetMode.Current, false, false, null))
            ];
        }

        return [CreateOpenModeResult(mode)];
    }

    private static IReadOnlyList<ResultDescriptor> BuildNewCurrentResult(
        ParsedQuery query)
    {
        var preview = query.Prompt is null ? string.Empty : $" · {Preview(query.Prompt)}";
        return
        [
            new ResultDescriptor(
                query.Prompt is null ? "在当前模式新建聊天" : "在当前模式新建聊天并粘贴 Prompt",
                $"新建聊天 · 自动粘贴，不自动发送{preview}",
                new ExecutionPlan(TargetMode.Current, false, true, query.Prompt))
        ];
    }

    private static ResultDescriptor CreatePromptResult(
        TargetMode mode,
        string prompt)
    {
        var modeText = mode == TargetMode.Current ? "当前模式" : ModeName(mode);
        var title = mode == TargetMode.Current
            ? "在当前模式新建聊天并使用此 Prompt"
            : $"在新 {modeText} 中使用此 Prompt";
        var action = mode == TargetMode.Current
            ? "新建聊天"
            : $"切换到 {modeText} 模式并新建聊天";

        return new ResultDescriptor(
            title,
            $"{action} · 自动粘贴，不自动发送 · {Preview(prompt)}",
            new ExecutionPlan(mode, mode != TargetMode.Current, true, prompt));
    }

    private static void AddUnique(ICollection<TargetMode> modes, TargetMode mode)
    {
        if (!modes.Contains(mode))
        {
            modes.Add(mode);
        }
    }

    private static string Preview(string prompt)
    {
        var singleLine = prompt
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Trim();

        return singleLine.Length <= PromptPreviewLimit
            ? singleLine
            : $"{singleLine[..PromptPreviewLimit]}…";
    }

    private static string ModeName(TargetMode mode) => mode switch
    {
        TargetMode.Chat => "Chat",
        TargetMode.Work => "Work",
        TargetMode.Codex => "Codex",
        TargetMode.Current => "当前模式",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "未知模式")
    };
}
