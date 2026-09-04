using FlowGPTBridge.Models;

namespace FlowGPTBridge.Parsing;

/// <summary>
/// 将 action keyword 后的文本解析为明确语义。此类无任何系统副作用。
/// </summary>
public sealed class CommandParser
{
    private static readonly Dictionary<string, TargetMode> ModeCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/chat"] = TargetMode.Chat,
        ["/c"] = TargetMode.Chat,
        ["/work"] = TargetMode.Work,
        ["/w"] = TargetMode.Work,
        ["/codex"] = TargetMode.Codex,
        ["/x"] = TargetMode.Codex,
        ["/current"] = TargetMode.Current,
        ["/o"] = TargetMode.Current
    };

    private static readonly HashSet<string> NewCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "/new",
        "/n"
    };

    public ParsedQuery Parse(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return OpenCurrent();
        }

        // Flow 通常会移除 action keyword 后的首个空格；这里仍显式忽略额外前导空白。
        var input = arguments.TrimStart();
        var tokenLength = FindFirstTokenLength(input);
        var firstToken = input[..tokenLength];

        if (firstToken == "--")
        {
            var escapedPrompt = GetRemainder(input, tokenLength);
            return string.IsNullOrWhiteSpace(escapedPrompt)
                ? OpenCurrent()
                : new ParsedQuery(null, escapedPrompt, true, QueryKind.Prompt);
        }

        if (ModeCommands.TryGetValue(firstToken, out var targetMode))
        {
            var prompt = NullIfWhiteSpace(GetRemainder(input, tokenLength));
            return new ParsedQuery(
                targetMode,
                prompt,
                prompt is not null,
                QueryKind.ExplicitMode);
        }

        if (NewCommands.Contains(firstToken))
        {
            var prompt = NullIfWhiteSpace(GetRemainder(input, tokenLength));
            return new ParsedQuery(
                TargetMode.Current,
                prompt,
                true,
                QueryKind.NewCurrent);
        }

        // 未知斜杠词和 chat/work/codex 等普通单词都必须保持为完整 Prompt。
        return new ParsedQuery(null, input, true, QueryKind.Prompt);
    }

    private static ParsedQuery OpenCurrent() =>
        new(TargetMode.Current, null, false, QueryKind.OpenCurrent);

    private static int FindFirstTokenLength(string input)
    {
        var index = 0;
        while (index < input.Length && !char.IsWhiteSpace(input[index]))
        {
            index++;
        }

        return index;
    }

    /// <summary>
    /// 只移除命令后的一个分隔空白，其余换行、Unicode 和缩进原样保留。
    /// </summary>
    private static string GetRemainder(string input, int tokenLength)
    {
        if (tokenLength >= input.Length)
        {
            return string.Empty;
        }

        var start = tokenLength;
        if (char.IsWhiteSpace(input[start]))
        {
            start++;
        }

        return input[start..];
    }

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
