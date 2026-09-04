using FlowGPTBridge.Models;
using FlowGPTBridge.Parsing;

namespace FlowGPTBridge.Tests;

public sealed class CommandParserTests
{
    private readonly CommandParser _parser = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t")]
    public void Empty_arguments_open_current_page(string? input)
    {
        var parsed = _parser.Parse(input);

        Assert.Equal(QueryKind.OpenCurrent, parsed.Kind);
        Assert.Equal(TargetMode.Current, parsed.ExplicitMode);
        Assert.Null(parsed.Prompt);
        Assert.False(parsed.CreateNewChat);
    }

    [Theory]
    [InlineData("帮我整理这段需求")]
    [InlineData("work")]
    [InlineData("chat")]
    [InlineData("codex")]
    [InlineData("/Deepresearch 调查市场")]
    public void Ordinary_and_unknown_command_text_is_a_prompt(string input)
    {
        var parsed = _parser.Parse(input);

        Assert.Equal(QueryKind.Prompt, parsed.Kind);
        Assert.Null(parsed.ExplicitMode);
        Assert.Equal(input, parsed.Prompt);
        Assert.True(parsed.CreateNewChat);
    }

    [Theory]
    [InlineData("/chat", TargetMode.Chat)]
    [InlineData("/C", TargetMode.Chat)]
    [InlineData("/work", TargetMode.Work)]
    [InlineData("/W", TargetMode.Work)]
    [InlineData("/codex", TargetMode.Codex)]
    [InlineData("/X", TargetMode.Codex)]
    public void Explicit_mode_without_prompt_only_switches_mode(string input, TargetMode expected)
    {
        var parsed = _parser.Parse(input);

        Assert.Equal(QueryKind.ExplicitMode, parsed.Kind);
        Assert.Equal(expected, parsed.ExplicitMode);
        Assert.Null(parsed.Prompt);
        Assert.False(parsed.CreateNewChat);
    }

    [Fact]
    public void Explicit_mode_with_prompt_creates_new_chat()
    {
        var parsed = _parser.Parse("/work 制作一份报告");

        Assert.Equal(TargetMode.Work, parsed.ExplicitMode);
        Assert.Equal("制作一份报告", parsed.Prompt);
        Assert.True(parsed.CreateNewChat);
    }

    [Theory]
    [InlineData("/new", null)]
    [InlineData("/n hello", "hello")]
    public void New_command_always_creates_in_current_mode(string input, string? prompt)
    {
        var parsed = _parser.Parse(input);

        Assert.Equal(QueryKind.NewCurrent, parsed.Kind);
        Assert.Equal(TargetMode.Current, parsed.ExplicitMode);
        Assert.Equal(prompt, parsed.Prompt);
        Assert.True(parsed.CreateNewChat);
    }

    [Fact]
    public void Current_with_prompt_creates_new_chat_without_switching_mode()
    {
        var parsed = _parser.Parse("/current 继续整理");

        Assert.Equal(QueryKind.ExplicitMode, parsed.Kind);
        Assert.Equal(TargetMode.Current, parsed.ExplicitMode);
        Assert.Equal("继续整理", parsed.Prompt);
        Assert.True(parsed.CreateNewChat);
    }

    [Fact]
    public void Double_dash_forces_reserved_words_to_be_prompt_text()
    {
        var parsed = _parser.Parse("-- /work 的含义");

        Assert.Equal(QueryKind.Prompt, parsed.Kind);
        Assert.Null(parsed.ExplicitMode);
        Assert.Equal("/work 的含义", parsed.Prompt);
    }

    [Fact]
    public void Prompt_preserves_unicode_newlines_and_indentation()
    {
        const string input = "/codex 修复：🚀\n    if (ok)\n        Run();";

        var parsed = _parser.Parse(input);

        Assert.Equal("修复：🚀\n    if (ok)\n        Run();", parsed.Prompt);
    }

    [Fact]
    public void Command_prefix_is_not_guessed()
    {
        var parsed = _parser.Parse("/workbench hello");

        Assert.Equal(QueryKind.Prompt, parsed.Kind);
        Assert.Equal("/workbench hello", parsed.Prompt);
    }

    [Fact]
    public void Whitespace_only_remainder_is_not_a_prompt()
    {
        var parsed = _parser.Parse("/work   \t");

        Assert.Null(parsed.Prompt);
        Assert.False(parsed.CreateNewChat);
    }
}
