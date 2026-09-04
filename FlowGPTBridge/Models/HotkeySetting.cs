using System.Windows.Input;

namespace FlowGPTBridge.Models;

/// <summary>
/// 可序列化快捷键。Key 保存 WPF Key 的名称，而不是仅供展示的字符串。
/// </summary>
public sealed class HotkeySetting : IEquatable<HotkeySetting>
{
    public List<string> Modifiers { get; set; } = [];

    public string? Key { get; set; }

    public static HotkeySetting Alt(string key) => new()
    {
        Modifiers = ["Alt"],
        Key = key
    };

    public static HotkeySetting Ctrl(string key) => new()
    {
        Modifiers = ["Ctrl"],
        Key = key
    };

    public bool IsEmpty => string.IsNullOrWhiteSpace(Key);

    public HotkeySetting Clone() => new()
    {
        Modifiers = [.. Modifiers],
        Key = Key
    };

    /// <summary>
    /// 返回稳定的比较键，修饰键顺序不会影响冲突检测。
    /// </summary>
    public string ToCanonicalString()
    {
        if (IsEmpty)
        {
            return string.Empty;
        }

        var parts = HotkeyFormatter.ModifierOrder
            .Where(modifier => Modifiers.Contains(modifier, StringComparer.OrdinalIgnoreCase))
            .ToList();
        parts.Add(Key!.ToUpperInvariant());

        return string.Join("+", parts);
    }

    public bool Equals(HotkeySetting? other) =>
        other is not null &&
        string.Equals(ToCanonicalString(), other.ToCanonicalString(), StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as HotkeySetting);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(ToCanonicalString());
}

/// <summary>
/// 快捷键展示与 WPF 键名转换的集中实现。
/// </summary>
public static class HotkeyFormatter
{
    public static readonly string[] ModifierOrder = ["Ctrl", "Alt", "Shift", "Win"];

    public static string Format(HotkeySetting? hotkey)
    {
        if (hotkey is null || hotkey.IsEmpty)
        {
            return "未设置";
        }

        var parts = ModifierOrder
            .Where(modifier => hotkey.Modifiers.Contains(modifier, StringComparer.OrdinalIgnoreCase))
            .ToList();

        parts.Add(FormatKeyName(hotkey.Key!));
        return string.Join("+", parts);
    }

    public static bool TryGetWpfKey(HotkeySetting hotkey, out Key key)
    {
        key = Key.None;
        if (hotkey.IsEmpty || hotkey.Modifiers.Any(modifier =>
                !ModifierOrder.Contains(modifier, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        try
        {
            var converted = new KeyConverter().ConvertFromInvariantString(hotkey.Key!);
            if (converted is not Key parsed || IsModifierKey(parsed))
            {
                return false;
            }

            key = parsed;
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    public static bool IsModifierKey(Key key) => key is
        System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl or
        System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt or
        System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift or
        System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin;

    private static string FormatKeyName(string key) => key switch
    {
        "D0" => "0",
        "D1" => "1",
        "D2" => "2",
        "D3" => "3",
        "D4" => "4",
        "D5" => "5",
        "D6" => "6",
        "D7" => "7",
        "D8" => "8",
        "D9" => "9",
        _ => key
    };
}
