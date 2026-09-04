using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using FlowGPTBridge.Models;

namespace FlowGPTBridge.Services;

/// <summary>
/// 使用 SendInput 串行发送快捷键。调用方必须提供前台身份验证函数。
/// </summary>
public sealed class ShortcutSender
{
    private static readonly int[] PhysicalModifierKeys =
    [
        NativeMethods.VkControl,
        NativeMethods.VkMenu,
        NativeMethods.VkShift,
        NativeMethods.VkLwin,
        NativeMethods.VkRwin
    ];

    public async Task<bool> WaitForPhysicalModifiersReleasedAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (PhysicalModifierKeys.All(key => !IsPhysicallyDown(key)))
            {
                return true;
            }

            await Task.Delay(25, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    public bool Send(HotkeySetting hotkey, Func<bool> isSafeTargetForeground)
    {
        if (!isSafeTargetForeground() || !TryBuildInputs(hotkey, out var inputs))
        {
            return false;
        }

        var sent = NativeMethods.SendInput(
            checked((uint)inputs.Length),
            inputs,
            Marshal.SizeOf<NativeMethods.Input>());

        if (sent == inputs.Length)
        {
            return true;
        }

        // SendInput 极少数情况下可能只处理部分输入。主动释放所有相关键，
        // 避免模拟修饰键残留到用户后续操作。
        ReleaseKeysBestEffort(hotkey);
        return false;
    }

    private static bool TryBuildInputs(HotkeySetting hotkey, out NativeMethods.Input[] inputs)
    {
        inputs = [];
        if (!HotkeyFormatter.TryGetWpfKey(hotkey, out var key))
        {
            return false;
        }

        var mainVirtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (mainVirtualKey <= 0)
        {
            return false;
        }

        var modifierKeys = GetModifierVirtualKeys(hotkey).ToList();
        var events = new List<NativeMethods.Input>(modifierKeys.Count * 2 + 2);

        events.AddRange(modifierKeys.Select(virtualKey => KeyboardInput(virtualKey, keyUp: false)));
        events.Add(KeyboardInput(mainVirtualKey, keyUp: false));
        events.Add(KeyboardInput(mainVirtualKey, keyUp: true));
        events.AddRange(modifierKeys.AsEnumerable().Reverse()
            .Select(virtualKey => KeyboardInput(virtualKey, keyUp: true)));

        inputs = [.. events];
        return true;
    }

    private static IEnumerable<int> GetModifierVirtualKeys(HotkeySetting hotkey)
    {
        foreach (var modifier in HotkeyFormatter.ModifierOrder)
        {
            if (!hotkey.Modifiers.Contains(modifier, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return modifier switch
            {
                "Ctrl" => NativeMethods.VkControl,
                "Alt" => NativeMethods.VkMenu,
                "Shift" => NativeMethods.VkShift,
                "Win" => NativeMethods.VkLwin,
                _ => throw new InvalidEnumArgumentException(nameof(modifier), 0, typeof(string))
            };
        }
    }

    private static NativeMethods.Input KeyboardInput(int virtualKey, bool keyUp) => new()
    {
        Type = NativeMethods.InputKeyboard,
        Data = new NativeMethods.InputUnion
        {
            Keyboard = new NativeMethods.KeyboardInput
            {
                VirtualKey = checked((ushort)virtualKey),
                Flags = keyUp ? NativeMethods.KeyeventfKeyup : 0
            }
        }
    };

    private static void ReleaseKeysBestEffort(HotkeySetting hotkey)
    {
        var virtualKeys = GetModifierVirtualKeys(hotkey).Reverse().ToList();
        if (HotkeyFormatter.TryGetWpfKey(hotkey, out var key))
        {
            virtualKeys.Insert(0, KeyInterop.VirtualKeyFromKey(key));
        }

        var releases = virtualKeys
            .Where(key => key > 0)
            .Select(key => KeyboardInput(key, keyUp: true))
            .ToArray();

        if (releases.Length > 0)
        {
            NativeMethods.SendInput(
                checked((uint)releases.Length),
                releases,
                Marshal.SizeOf<NativeMethods.Input>());
        }
    }

    private static bool IsPhysicallyDown(int virtualKey) =>
        (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
}
