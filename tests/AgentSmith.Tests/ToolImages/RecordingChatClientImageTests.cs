using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.ToolImages;

/// <summary>
/// 2026-10-01-283dd: a traced image never reaches the trace as base64. 2026-11-09-0620d: the trace holds no prompt at
/// all — every prompt is stored deduplicated, where an image is its media type, size and hash (PromptBlocksTests).
/// </summary>
public sealed class RecordingChatClientImageTests
{
    [Fact]
    public async Task RecordingChatClient_TracedCall_RecordsTheAnswerAndNoPrompt()
    {
        var png = ToolImageLoopFixture.Png(10, 10);
        var trace = new CapturingTrace();
        var client = new RecordingChatClient(new ScriptedToolChat(), trace, EventTestStubs.RunContext);

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, [new TextContent("[image from render]"), new DataContent(png, "image/png")])]);

        trace.Labels.Should().Equal(RecordedTrace.AnswerLabel);
        trace.Entries.Should().NotContain(e => e.Contains("[image from render]") || e.Contains(Convert.ToBase64String(png)));
    }

    private sealed class CapturingTrace : IRunTraceWriter
    {
        public List<string> Entries { get; } = [];
        public List<string> Labels { get; } = [];
        public bool IsEnabled => true;

        public Task WriteAsync(string runId, string label, string content, CancellationToken cancellationToken)
        {
            Labels.Add(label);
            Entries.Add(content);
            return Task.CompletedTask;
        }
    }
}
