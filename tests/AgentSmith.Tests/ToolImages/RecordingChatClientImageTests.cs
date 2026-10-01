using System.Security.Cryptography;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.ToolImages;

/// <summary>2026-10-01-283dd: a traced image leaves its media type, size and hash — never base64.</summary>
public sealed class RecordingChatClientImageTests
{
    [Fact]
    public async Task RecordingChatClient_Image_IsRecordedAsSizeAndSha256()
    {
        var png = ToolImageLoopFixture.Png(10, 10);
        var trace = new CapturingTrace();
        var client = new RecordingChatClient(new ScriptedToolChat(), trace, EventTestStubs.RunContext);

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, [new TextContent("[image from render]"), new DataContent(png, "image/png")])]);

        var prompt = trace.Entries[0];
        prompt.Should().Contain("[image from render]")
            .And.Contain($"[image image/png, 64 bytes, sha256 {Convert.ToHexStringLower(SHA256.HashData(png))}]");
        prompt.Should().NotContain(Convert.ToBase64String(png));
    }

    private sealed class CapturingTrace : IRunTraceWriter
    {
        public List<string> Entries { get; } = [];
        public bool IsEnabled => true;

        public Task WriteAsync(string runId, string label, string content, CancellationToken cancellationToken)
        {
            Entries.Add(content);
            return Task.CompletedTask;
        }
    }
}
