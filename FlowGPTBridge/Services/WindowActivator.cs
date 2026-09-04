using System.Diagnostics;

namespace FlowGPTBridge.Services;

/// <summary>
/// 恢复并激活目标窗口，且在发送快捷键前验证前台进程身份。
/// </summary>
public sealed class WindowActivator
{
    private readonly ChatGptLauncher _launcher;
    private readonly Action<string> _debugLog;

    public WindowActivator(ChatGptLauncher launcher, Action<string> debugLog)
    {
        _launcher = launcher;
        _debugLog = debugLog;
    }

    public async Task<bool> ActivateAsync(
        ChatGptWindow window,
        CancellationToken cancellationToken)
    {
        if (NativeMethods.IsIconic(window.Handle))
        {
            NativeMethods.ShowWindow(window.Handle, NativeMethods.SwRestore);
        }

        var foreground = NativeMethods.GetForegroundWindow();
        var foregroundThread = foreground == nint.Zero
            ? 0
            : NativeMethods.GetWindowThreadProcessId(foreground, out _);
        var targetThread = NativeMethods.GetWindowThreadProcessId(window.Handle, out _);
        var currentThread = NativeMethods.GetCurrentThreadId();

        var attachedForeground = false;
        var attachedTarget = false;
        try
        {
            if (foregroundThread != 0 && foregroundThread != currentThread)
            {
                attachedForeground = NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
            }

            if (targetThread != 0 &&
                targetThread != currentThread &&
                targetThread != foregroundThread)
            {
                attachedTarget = NativeMethods.AttachThreadInput(currentThread, targetThread, true);
            }

            NativeMethods.BringWindowToTop(window.Handle);
            NativeMethods.SetForegroundWindow(window.Handle);
            NativeMethods.SetFocus(window.Handle);
        }
        finally
        {
            if (attachedTarget)
            {
                NativeMethods.AttachThreadInput(currentThread, targetThread, false);
            }

            if (attachedForeground)
            {
                NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
            }
        }

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < 1200)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsForegroundChatGpt())
            {
                _debugLog($"ChatGPT 已置于前台，耗时 {stopwatch.ElapsedMilliseconds} ms。");
                return true;
            }

            await Task.Delay(40, cancellationToken).ConfigureAwait(false);
        }

        _debugLog("未能确认 ChatGPT 成为前台窗口。");
        return false;
    }

    public bool IsForegroundChatGpt()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == nint.Zero)
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(foreground, out var processId);
        return processId != 0 && _launcher.IsChatGptProcess(processId);
    }
}
