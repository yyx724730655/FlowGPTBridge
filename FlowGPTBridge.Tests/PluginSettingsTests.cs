using FlowGPTBridge.Models;

namespace FlowGPTBridge.Tests;

public sealed class PluginSettingsTests
{
    [Fact]
    public void Defaults_match_chatgpt_shortcuts()
    {
        var settings = new PluginSettings();

        Assert.Equal("Alt+1", HotkeyFormatter.Format(settings.ChatShortcut));
        Assert.Equal("Alt+2", HotkeyFormatter.Format(settings.WorkShortcut));
        Assert.Equal("Alt+3", HotkeyFormatter.Format(settings.CodexShortcut));
        Assert.Equal("Ctrl+N", HotkeyFormatter.Format(settings.NewChatShortcut));
        Assert.Equal(TargetMode.Chat, settings.DefaultPromptMode);
        Assert.Equal(8000, settings.LaunchTimeoutMs);
        Assert.False(settings.DebugLogging);
    }

    [Fact]
    public void Modifier_order_does_not_affect_conflict_detection()
    {
        var settings = new PluginSettings
        {
            ChatShortcut = new HotkeySetting { Modifiers = ["Shift", "Ctrl"], Key = "K" },
            WorkShortcut = new HotkeySetting { Modifiers = ["Ctrl", "Shift"], Key = "K" }
        };

        var conflict = Assert.Single(settings.FindHotkeyConflicts());

        Assert.Contains("Chat", conflict);
        Assert.Contains("Work", conflict);
    }

    [Fact]
    public void Empty_shortcuts_do_not_conflict()
    {
        var settings = new PluginSettings
        {
            ChatShortcut = new HotkeySetting(),
            WorkShortcut = new HotkeySetting()
        };

        Assert.Empty(settings.FindHotkeyConflicts());
    }
}
