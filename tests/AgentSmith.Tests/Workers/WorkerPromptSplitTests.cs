using AgentSmith.Infrastructure.Services.Workers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Workers;

/// <summary>
/// 2026-11-17-166ae: a worker call's prompt in two parts. The system part is the same on every call of a conversation
/// — a Claude Code CLI reads it from --system-prompt-file and serves it from cache on the next call — and the input
/// part carries what changes. Measured in the replay: 164 of 172 calls read only the CLI's own prompt from cache.
/// </summary>
public sealed class WorkerPromptSplitTests
{
    private static readonly WorkerJsonFormat Json = new();

    [Fact]
    public void Render_TwoCallsOfOneConversation_ShareTheSystemPartByteForByte()
    {
        var tool = AIFunctionFactory.Create((string path) => "ok", "read_file", "Reads a file");
        var options = new ChatOptions { Tools = [tool] };
        List<ChatMessage> first = [new(ChatRole.System, "You are the coding master."), new(ChatRole.User, "Build it.")];
        List<ChatMessage> second = [.. first, new(ChatRole.Assistant, "Reading."), new(ChatRole.User, "Go on.")];

        var a = Render(first, options, step: 7);
        var b = Render(second, options, step: 8);

        b.System.Should().Be(a.System, "the system part is the cached prefix of the next call");
        a.System.Should().Contain("You are the coding master.").And.Contain("read_file").And.Contain("input_schema");
        a.Input.Should().NotContain("read_file").And.NotContain("You are the coding master.");
        b.Input.Should().Contain("request_id").And.Contain("Go on.").And.Contain("\"step_index\": 8");
        a.System.IndexOf("You are the coding master.", StringComparison.Ordinal)
            .Should().BeLessThan(a.System.IndexOf("read_file", StringComparison.Ordinal),
                "the system messages precede the tools, so a changed tool list leaves them a cached prefix");
    }

    [Fact]
    public void Render_ASystemMessageAfterTheConversationStarted_StaysInTheInput()
    {
        List<ChatMessage> chat = [new(ChatRole.System, "lead"), new(ChatRole.User, "u"), new(ChatRole.System, "a nudge")];

        var prompt = Render(chat, new ChatOptions(), step: 1);

        prompt.System.Should().Contain("lead").And.NotContain("a nudge");
        prompt.Input.Should().Contain("a nudge");
        prompt.Text.Should().Be(prompt.System + prompt.Input);
        prompt.Length.Should().Be(prompt.Text.Length);
    }

    [Fact]
    public async Task Run_ClaudeCodeBinary_ReadsTheSystemPartFromAFileItRemovesAfterwards()
    {
        if (OperatingSystem.IsWindows()) return;
        // sh -c '<script>' --system-prompt-file <path>: the two appended arguments arrive as $0 and $1.
        var options = new ExternalWorkerCliOptions("/bin/sh",
            ["-c", "in=$(cat); printf '%s|%s|' \"$0\" \"$1\"; cat \"$1\"; printf '|%s' \"$in\""],
            TimeSpan.FromSeconds(30), Path.GetTempPath()) { SystemPromptAsFile = true };

        var result = await Runner().RunAsync(new WorkerPrompt("SYSTEM PART", "INPUT PART"), options, CancellationToken.None);

        var parts = result.StandardOutput.Split('|');
        parts[0].Should().Be("--system-prompt-file");
        parts[2].Should().Be("SYSTEM PART");
        parts[3].Should().Be("INPUT PART", "only the input part goes on stdin");
        File.Exists(parts[1]).Should().BeFalse("the file lives for one call");
    }

    [Fact]
    public async Task Run_PlainBinary_ReadsBothPartsOnStdin()
    {
        if (OperatingSystem.IsWindows()) return;
        var options = new ExternalWorkerCliOptions("/bin/sh", ["-c", "cat"], TimeSpan.FromSeconds(30), Path.GetTempPath());

        var result = await Runner().RunAsync(new WorkerPrompt("SYSTEM PART ", "INPUT PART"), options, CancellationToken.None);

        result.StandardOutput.Should().Be("SYSTEM PART INPUT PART");
    }

    private static WorkerPrompt Render(List<ChatMessage> chat, ChatOptions options, int step) =>
        new WorkerPromptRenderer(Json).Render(
            new WorkerRequestComposer(new WorkerMessageMapper(Json), new WorkerOptionsMapper())
                .Compose(chat, options, new("run-42", step, "coding-master", "Implementation", "primary", "external_worker", "sonnet")));

    private static AgentCliWorkerProcessRunner Runner() => new(NullLogger<AgentCliWorkerProcessRunner>.Instance);
}
