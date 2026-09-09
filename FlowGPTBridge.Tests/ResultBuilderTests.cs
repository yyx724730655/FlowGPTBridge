using FlowGPTBridge.Models;
using FlowGPTBridge.Parsing;
using FlowGPTBridge.Results;

namespace FlowGPTBridge.Tests;

public sealed class ResultBuilderTests
{
    private readonly CommandParser _parser = new();
    private readonly ResultBuilder _builder = new();

    [Fact]
    public void Empty_query_keeps_current_page_first()
    {
        var results = _builder.Build(_parser.Parse(string.Empty), new PluginSettings());

        Assert.Equal(4, results.Count);
        Assert.Contains("保持当前模式", results[0].Title);
        Assert.False(results[0].Plan.SwitchMode);
        Assert.False(results[0].Plan.CreateNewChat);

        Assert.Equal("打开 ChatGPT — Chat 模式", results[1].Title);
        Assert.Equal("切换到 Chat 模式，不新建聊天", results[1].SubTitle);
        Assert.Equal("打开 ChatGPT — Work 模式", results[2].Title);
        Assert.Equal("切换到 Work 模式，不新建聊天", results[2].SubTitle);
        Assert.Equal("打开 ChatGPT — Codex 模式", results[3].Title);
        Assert.Equal("切换到 Codex 模式，不新建聊天", results[3].SubTitle);
    }

    [Theory]
    [InlineData(TargetMode.Chat)]
    [InlineData(TargetMode.Work)]
    [InlineData(TargetMode.Codex)]
    [InlineData(TargetMode.Current)]
    public void Default_prompt_mode_is_first(TargetMode defaultMode)
    {
        var settings = new PluginSettings { DefaultPromptMode = defaultMode };
        var results = _builder.Build(_parser.Parse("hello"), settings);

        Assert.Equal(defaultMode, results[0].Plan.TargetMode);
        Assert.All(results, result => Assert.True(result.Plan.CreateNewChat));
        Assert.All(results, result => Assert.Equal("hello", result.Plan.Prompt));
        Assert.Equal(defaultMode == TargetMode.Current ? 4 : 3, results.Count);
    }

    [Fact]
    public void Explicit_mode_returns_only_that_mode()
    {
        var results = _builder.Build(_parser.Parse("/work hello"), new PluginSettings());

        var result = Assert.Single(results);
        Assert.Equal(TargetMode.Work, result.Plan.TargetMode);
        Assert.True(result.Plan.SwitchMode);
        Assert.True(result.Plan.CreateNewChat);
    }

    [Fact]
    public void Current_command_returns_only_current_mode()
    {
        var result = Assert.Single(
            _builder.Build(_parser.Parse("/current hello"), new PluginSettings()));

        Assert.Equal(TargetMode.Current, result.Plan.TargetMode);
        Assert.False(result.Plan.SwitchMode);
        Assert.True(result.Plan.CreateNewChat);
    }

    [Fact]
    public void Long_prompt_is_truncated_to_one_line_in_subtitle()
    {
        var prompt = new string('a', 90) + "\nsecond line";
        var result = _builder.Build(_parser.Parse(prompt), new PluginSettings())[0];

        Assert.DoesNotContain('\n', result.SubTitle);
        Assert.Contains('…', result.SubTitle);
        Assert.DoesNotContain(prompt, result.SubTitle);
        Assert.Equal(prompt, result.Plan.Prompt);
    }

    [Fact]
    public void Subtitle_does_not_show_internal_shortcuts()
    {
        var settings = new PluginSettings
        {
            WorkShortcut = new HotkeySetting { Modifiers = ["Ctrl", "Shift"], Key = "K" },
            NewChatShortcut = HotkeySetting.Alt("N")
        };

        var result = Assert.Single(_builder.Build(_parser.Parse("/work hello"), settings));

        Assert.DoesNotContain("Ctrl+Shift+K", result.SubTitle);
        Assert.DoesNotContain("Alt+N", result.SubTitle);
        Assert.Equal("切换到 Work 模式并新建聊天 · 自动粘贴，不自动发送 · hello", result.SubTitle);
    }

    [Fact]
    public void Prompt_subtitle_describes_automatic_paste_without_send()
    {
        var result = _builder.Build(_parser.Parse("hello"), new PluginSettings())[0];

        Assert.Contains("自动粘贴", result.SubTitle);
        Assert.Contains("不自动发送", result.SubTitle);
    }
}
