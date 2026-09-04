using FlowGPTBridge.Models;
using FlowGPTBridge.Parsing;
using FlowGPTBridge.Results;

namespace FlowGPTBridge.Tests;

public sealed class ExecutionPlanTests
{
    private readonly CommandParser _parser = new();
    private readonly ResultBuilder _builder = new();
    private readonly PluginSettings _settings = new();

    [Fact]
    public void Gpt_first_item_has_no_shortcut_and_no_prompt()
    {
        var plan = Plans(string.Empty)[0];

        Assert.False(plan.SwitchMode);
        Assert.False(plan.CreateNewChat);
        Assert.Null(plan.Prompt);
    }

    [Fact]
    public void Gpt_chat_item_switches_without_new_chat()
    {
        var plan = Plans(string.Empty).Single(plan => plan.TargetMode == TargetMode.Chat);

        Assert.True(plan.SwitchMode);
        Assert.False(plan.CreateNewChat);
        Assert.Null(plan.Prompt);
    }

    [Fact]
    public void Work_command_only_switches_work()
    {
        var plan = Assert.Single(Plans("/work"));

        Assert.Equal(TargetMode.Work, plan.TargetMode);
        Assert.True(plan.SwitchMode);
        Assert.False(plan.CreateNewChat);
    }

    [Fact]
    public void Ordinary_prompt_in_chat_switches_creates_and_copies()
    {
        var plan = Plans("hello")[0];

        Assert.Equal(TargetMode.Chat, plan.TargetMode);
        Assert.True(plan.SwitchMode);
        Assert.True(plan.CreateNewChat);
        Assert.Equal("hello", plan.Prompt);
    }

    [Fact]
    public void Explicit_codex_prompt_switches_creates_and_copies()
    {
        var plan = Assert.Single(Plans("/codex fix tests"));

        Assert.Equal(TargetMode.Codex, plan.TargetMode);
        Assert.True(plan.SwitchMode);
        Assert.True(plan.CreateNewChat);
        Assert.Equal("fix tests", plan.Prompt);
    }

    [Theory]
    [InlineData("/new", null)]
    [InlineData("/new hello", "hello")]
    public void New_command_never_switches_mode(string input, string? prompt)
    {
        var plan = Assert.Single(Plans(input));

        Assert.Equal(TargetMode.Current, plan.TargetMode);
        Assert.False(plan.SwitchMode);
        Assert.True(plan.CreateNewChat);
        Assert.Equal(prompt, plan.Prompt);
    }

    private IReadOnlyList<ExecutionPlan> Plans(string input) =>
        _builder.Build(_parser.Parse(input), _settings)
            .Select(result => result.Plan)
            .ToList();
}
