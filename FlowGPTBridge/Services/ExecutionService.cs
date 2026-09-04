using FlowGPTBridge.Models;

namespace FlowGPTBridge.Services;

/// <summary>
/// 严格按开发说明串联完整状态机。执行锁确保快速重复触发时按键不会交错。
/// </summary>
public sealed class ExecutionService
{
    private static readonly SemaphoreSlim ExecutionLock = new(1, 1);
    private readonly PluginSettings _settings;
    private readonly ChatGptLauncher _launcher;
    private readonly WindowActivator _windowActivator;
    private readonly ShortcutSender _shortcutSender;
    private readonly ClipboardService _clipboardService;
    private readonly Action<string> _debugLog;

    public ExecutionService(
        PluginSettings settings,
        ChatGptLauncher launcher,
        WindowActivator windowActivator,
        ShortcutSender shortcutSender,
        ClipboardService clipboardService,
        Action<string> debugLog)
    {
        _settings = settings;
        _launcher = launcher;
        _windowActivator = windowActivator;
        _shortcutSender = shortcutSender;
        _clipboardService = clipboardService;
        _debugLog = debugLog;
    }

    public async Task<ExecutionResult> ExecuteAsync(
        ExecutionPlan plan,
        CancellationToken cancellationToken = default)
    {
        await ExecutionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var validation = ValidatePlan(plan);
            if (validation is not null)
            {
                return validation;
            }

            if (!await _shortcutSender.WaitForPhysicalModifiersReleasedAsync(
                    TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false))
            {
                return ExecutionResult.Fail(
                    "操作已停止",
                    "检测到 Ctrl、Alt、Shift 或 Win 仍被按住，未向任何窗口发送快捷键。");
            }

            var window = await _launcher.GetOrLaunchAsync(cancellationToken).ConfigureAwait(false);
            if (window is null)
            {
                return ExecutionResult.Fail(
                    "未找到 ChatGPT",
                    "请安装 ChatGPT，或在插件设置中配置启动路径/AppUserModelID 后重试。");
            }

            if (!await _windowActivator.ActivateAsync(window, cancellationToken).ConfigureAwait(false))
            {
                return ExecutionResult.Fail(
                    "ChatGPT 已打开",
                    "无法确认它已处于前台，因此没有发送快捷键。");
            }

            if (plan.SwitchMode)
            {
                var modeShortcut = _settings.GetModeShortcut(plan.TargetMode)!;
                if (!_shortcutSender.Send(modeShortcut, _windowActivator.IsForegroundChatGpt))
                {
                    return ExecutionResult.Fail(
                        "ChatGPT 已打开",
                        "模式快捷键未发送；请检查焦点和插件快捷键设置。");
                }

                _debugLog($"已发送 {plan.TargetMode} 模式快捷键。");
                await Task.Delay(180, cancellationToken).ConfigureAwait(false);
            }

            if (plan.CreateNewChat)
            {
                if (!_shortcutSender.Send(
                        _settings.NewChatShortcut,
                        _windowActivator.IsForegroundChatGpt))
                {
                    return ExecutionResult.Fail(
                        plan.SwitchMode ? "已切换模式" : "ChatGPT 已打开",
                        "新聊天快捷键未发送，因此没有复制 Prompt。");
                }

                _debugLog("已发送新聊天快捷键。");
            }

            if (plan.Prompt is not null)
            {
                if (!await _clipboardService.SetUnicodeTextAsync(plan.Prompt, cancellationToken)
                        .ConfigureAwait(false))
                {
                    return ExecutionResult.Fail(
                        "新聊天已打开",
                        "Prompt 未能写入剪贴板，请手动复制后再粘贴。");
                }

                return ExecutionResult.Ok(
                    "Prompt 已复制",
                    "请在 ChatGPT 中手动粘贴；插件不会自动粘贴或发送。");
            }

            if (plan.CreateNewChat)
            {
                return ExecutionResult.Ok("已新建聊天", "未执行粘贴或发送。");
            }

            if (plan.SwitchMode)
            {
                return ExecutionResult.Ok($"已打开 {plan.TargetMode}", "未新建聊天。");
            }

            return ExecutionResult.Ok("ChatGPT 已打开", "已保持上次页面。");
        }
        catch (OperationCanceledException)
        {
            return ExecutionResult.Fail("操作已取消", "没有继续发送快捷键。");
        }
        catch (Exception exception)
        {
            _debugLog($"执行异常：{exception.GetType().Name}，HRESULT=0x{exception.HResult:X8}。");
            return ExecutionResult.Fail("操作已停止", "发生系统错误，没有继续发送快捷键。");
        }
        finally
        {
            ExecutionLock.Release();
        }
    }

    public async Task<ExecutionResult> TestShortcutAsync(
        HotkeySetting hotkey,
        CancellationToken cancellationToken = default)
    {
        await ExecutionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsValidHotkey(hotkey))
            {
                return ExecutionResult.Fail("快捷键无效", "请先录制一个包含普通键的快捷键。");
            }

            if (!await _shortcutSender.WaitForPhysicalModifiersReleasedAsync(
                    TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false))
            {
                return ExecutionResult.Fail("测试已停止", "请先松开 Ctrl、Alt、Shift 和 Win。");
            }

            var window = await _launcher.GetOrLaunchAsync(cancellationToken).ConfigureAwait(false);
            if (window is null ||
                !await _windowActivator.ActivateAsync(window, cancellationToken).ConfigureAwait(false))
            {
                return ExecutionResult.Fail("测试已停止", "无法确认 ChatGPT 已处于前台。");
            }

            return _shortcutSender.Send(hotkey, _windowActivator.IsForegroundChatGpt)
                ? ExecutionResult.Ok("快捷键已发送", "测试不会粘贴或发送消息。")
                : ExecutionResult.Fail("测试失败", "焦点验证失败或快捷键无法发送。");
        }
        catch (OperationCanceledException)
        {
            return ExecutionResult.Fail("测试已取消", "没有发送快捷键。");
        }
        finally
        {
            ExecutionLock.Release();
        }
    }

    private ExecutionResult? ValidatePlan(ExecutionPlan plan)
    {
        var conflicts = _settings.FindHotkeyConflicts();
        if (conflicts.Count > 0)
        {
            return ExecutionResult.Fail(
                "快捷键设置冲突",
                $"请在插件设置中修改：{string.Join("；", conflicts)}。");
        }

        if (plan.SwitchMode && !IsValidHotkey(_settings.GetModeShortcut(plan.TargetMode)))
        {
            return ExecutionResult.Fail("模式快捷键无效", "请在插件设置中重新录制该快捷键。");
        }

        if (plan.CreateNewChat && !IsValidHotkey(_settings.NewChatShortcut))
        {
            return ExecutionResult.Fail("新聊天快捷键无效", "未复制 Prompt，请先修正插件设置。");
        }

        if (plan.Prompt is not null && !plan.CreateNewChat)
        {
            return ExecutionResult.Fail("执行计划无效", "含 Prompt 的操作必须先新建聊天。");
        }

        return null;
    }

    private static bool IsValidHotkey(HotkeySetting? hotkey) =>
        hotkey is not null && HotkeyFormatter.TryGetWpfKey(hotkey, out _);
}
