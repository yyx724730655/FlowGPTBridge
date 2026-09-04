using System.Diagnostics;
using System.Text;
using FlowGPTBridge.Models;

namespace FlowGPTBridge.Services;

/// <summary>
/// 封装 ChatGPT 进程识别、窗口发现与启动方式。
/// 易变化的应用身份信息不会泄漏到执行状态机中。
/// </summary>
public sealed class ChatGptLauncher
{
    private static readonly string[] KnownProcessNames = ["ChatGPT"];
    private readonly PluginSettings _settings;
    private readonly Action<string> _debugLog;

    public ChatGptLauncher(PluginSettings settings, Action<string> debugLog)
    {
        _settings = settings;
        _debugLog = debugLog;
    }

    public async Task<ChatGptWindow?> GetOrLaunchAsync(CancellationToken cancellationToken)
    {
        var existing = FindBestWindow();
        if (existing is not null)
        {
            return existing;
        }

        _debugLog("未找到可见 ChatGPT 主窗口，准备启动或重新激活应用。");
        if (!await TryLaunchAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return await WaitForWindowAsync(_settings.LaunchTimeoutMs, cancellationToken)
            .ConfigureAwait(false);
    }

    public ChatGptWindow? FindBestWindow()
    {
        var windows = new List<ChatGptWindow>();

        NativeMethods.EnumWindows((handle, _) =>
        {
            if (!NativeMethods.IsWindowVisible(handle))
            {
                return true;
            }

            NativeMethods.GetWindowThreadProcessId(handle, out var processId);
            if (processId != 0 && IsChatGptProcess(processId))
            {
                windows.Add(new ChatGptWindow(handle, processId));
            }

            return true;
        }, nint.Zero);

        // 主窗口通常是第一个可见顶层窗口；最小化窗口仍被 IsWindowVisible 返回。
        return windows.FirstOrDefault();
    }

    public bool IsChatGptProcess(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            if (KnownProcessNames.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(_settings.ChatGptExecutablePath))
            {
                return false;
            }

            var configuredPath = Path.GetFullPath(_settings.ChatGptExecutablePath);
            var processPath = process.MainModule?.FileName;
            return processPath is not null &&
                   string.Equals(
                       Path.GetFullPath(processPath),
                       configuredPath,
                       StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is
            ArgumentException or
            InvalidOperationException or
            System.ComponentModel.Win32Exception or
            NotSupportedException)
        {
            return false;
        }
    }

    private async Task<ChatGptWindow?> WaitForWindowAsync(
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var boundedTimeout = Math.Clamp(timeoutMs, 1000, 60000);

        while (stopwatch.ElapsedMilliseconds < boundedTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var window = FindBestWindow();
            if (window is not null)
            {
                _debugLog($"ChatGPT 窗口已出现，耗时 {stopwatch.ElapsedMilliseconds} ms。");
                return window;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        _debugLog($"等待 ChatGPT 窗口超时，耗时 {stopwatch.ElapsedMilliseconds} ms。");
        return null;
    }

    private async Task<bool> TryLaunchAsync(CancellationToken cancellationToken)
    {
        if (TryLaunchConfiguredExecutable())
        {
            return true;
        }

        var configuredAppId = _settings.ChatGptAppUserModelId?.Trim();
        if (!string.IsNullOrWhiteSpace(configuredAppId) && TryLaunchAppId(configuredAppId))
        {
            return true;
        }

        var registeredAppId = await TryResolveRegisteredAppIdAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(registeredAppId) && TryLaunchAppId(registeredAppId))
        {
            return true;
        }

        return TryLaunchKnownExecutableLocations();
    }

    private bool TryLaunchConfiguredExecutable()
    {
        var configuredPath = _settings.ChatGptExecutablePath?.Trim();
        return !string.IsNullOrWhiteSpace(configuredPath) &&
               File.Exists(configuredPath) &&
               TryStartProcess(configuredPath, null);
    }

    private bool TryLaunchKnownExecutableLocations()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            Path.Combine(localAppData, "Programs", "ChatGPT", "ChatGPT.exe"),
            Path.Combine(localAppData, "ChatGPT", "ChatGPT.exe")
        };

        return candidates.Any(path => File.Exists(path) && TryStartProcess(path, null));
    }

    private static bool TryLaunchAppId(string appUserModelId) =>
        TryStartProcess("explorer.exe", $"shell:AppsFolder\\{appUserModelId}");

    /// <summary>
    /// 从 Windows 开始菜单的应用注册信息解析 ChatGPT 的 AppUserModelID。
    /// PowerShell 仅用于读取系统注册结果，不读取 ChatGPT 私有配置。
    /// </summary>
    private async Task<string?> TryResolveRegisteredAppIdAsync(CancellationToken cancellationToken)
    {
        const string script =
            "$app = Get-StartApps | Where-Object { $_.Name -eq 'ChatGPT' } | Select-Object -First 1; " +
            "if ($null -ne $app) { [Console]::Out.Write($app.AppID) }";

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoLogo -NoProfile -NonInteractive -EncodedCommand {encoded}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var output = (await outputTask.ConfigureAwait(false)).Trim();
            return process.ExitCode == 0 && output.Length > 0 ? output : null;
        }
        catch (Exception exception) when (exception is
            InvalidOperationException or
            System.ComponentModel.Win32Exception or
            OperationCanceledException)
        {
            _debugLog($"读取 Windows 应用注册信息失败：{exception.GetType().Name}。");
            return null;
        }
    }

    private static bool TryStartProcess(string fileName, string? arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = true
            };

            if (!string.IsNullOrWhiteSpace(arguments))
            {
                startInfo.Arguments = arguments;
            }

            return Process.Start(startInfo) is not null;
        }
        catch (Exception exception) when (exception is
            InvalidOperationException or
            System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
