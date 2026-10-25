using System.Text.Json;
using AgentSmith.Application.Services.Loop.Sanity;
using AgentSmith.Contracts.Runs;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Sanity;

/// <summary>2026-10-25-c5a7i: a write carrying the provider's secret placeholder is refused and never runs.</summary>
public sealed class MaskedPlaceholderWriteTests
{
    [Fact]
    public async Task MaskedPlaceholderWrite_MultiEditNestedNewString_IsRefused()
    {
        var calls = 0;
        var tool = new MaskedPlaceholderWrite(Tool("multi_edit", () => calls++));
        var edits = JsonSerializer.SerializeToElement(new[]
        {
            new { old_string = "a", new_string = "b" },
            new { old_string = "token=x", new_string = $"token={SecretMasker.ProviderPlaceholder}" },
        });

        var result = await tool.InvokeAsync(new AIFunctionArguments { ["path"] = ".npmrc", ["edits"] = edits });

        result!.ToString().Should().StartWith("Error: multi_edit was refused").And.Contain("run_command");
        calls.Should().Be(0);
    }

    [Fact]
    public async Task MaskedPlaceholderWrite_OrdinaryWrite_Passes()
    {
        var calls = 0;
        var tool = new MaskedPlaceholderWrite(Tool("write_file", () => calls++));

        await tool.InvokeAsync(new AIFunctionArguments { ["path"] = "a.txt", ["content"] = "hello *** world" });

        calls.Should().Be(1);
    }

    [Fact]
    public async Task MaskedPlaceholderWrite_ReadTool_IsNeverJudged()
    {
        var calls = 0;
        var tool = new MaskedPlaceholderWrite(Tool("grep", () => calls++));

        await tool.InvokeAsync(new AIFunctionArguments { ["pattern"] = SecretMasker.ProviderPlaceholder });

        calls.Should().Be(1);
    }

    private static AIFunction Tool(string name, Action invoked) =>
        AIFunctionFactory.Create(() => { invoked(); return "ok"; }, name);
}
