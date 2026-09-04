using System.Windows.Controls;
using Flow.Launcher.Plugin;
using FlowGPTBridge.Models;
using FlowGPTBridge.Parsing;
using FlowGPTBridge.Results;
using FlowGPTBridge.Services;
using FlowGPTBridge.Settings;

namespace FlowGPTBridge;

/// <summary>
/// Flow Launcher 插件入口。Query 只生成列表，只有结果 Action 才启动执行状态机。
/// </summary>
public sealed class Plugin : IPlugin, ISettingProvider
{
    private const string IconPath = "Images/icon.png";
    private readonly CommandParser _parser = new();
    private readonly ResultBuilder _resultBuilder = new();
    private PluginInitContext _context = null!;
    private PluginSettings _settings = null!;
    private ExecutionService _executionService = null!;

    public void Init(PluginInitContext context)
    {
        _context = context;
        _settings = context.API.LoadSettingJsonStorage<PluginSettings>();

        var launcher = new ChatGptLauncher(_settings, DebugLog);
        var activator = new WindowActivator(launcher, DebugLog);
        _executionService = new ExecutionService(
            _settings,
            launcher,
            activator,
            new ShortcutSender(),
            new ClipboardService(),
            DebugLog);
    }

    public List<Result> Query(Query query)
    {
        var parsed = _parser.Parse(query.Search);
        var descriptors = _resultBuilder.Build(parsed, _settings);

        return descriptors.Select(descriptor => new Result
        {
            Title = descriptor.Title,
            SubTitle = descriptor.SubTitle,
            IcoPath = IconPath,
            Action = actionContext =>
            {
                // 先隐藏 Flow，随后异步状态机等待用户松开呼出热键。
                _context.API.HideMainWindow();
                _ = ExecuteAndNotifyAsync(descriptor.Plan);
                return true;
            }
        }).ToList();
    }

    public Control CreateSettingPanel() => new SettingsControl(
        _settings,
        SaveSettings,
        hotkey => _executionService.TestShortcutAsync(hotkey));

    private async Task ExecuteAndNotifyAsync(ExecutionPlan plan)
    {
        var result = await _executionService.ExecuteAsync(plan).ConfigureAwait(false);

        // Flow 的消息 API 会自行调度 UI；消息中不包含 Prompt。
        _context.API.ShowMsg(result.Title, result.Message, IconPath);
    }

    private void SaveSettings() => _context.API.SaveSettingJsonStorage<PluginSettings>();

    private void DebugLog(string message)
    {
        if (_settings.DebugLogging)
        {
            _context.API.LogInfo(nameof(Plugin), message);
        }
    }
}
