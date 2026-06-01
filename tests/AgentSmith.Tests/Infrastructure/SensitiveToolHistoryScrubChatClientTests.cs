using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Infrastructure;

/// <summary>
/// p0191: history-scrub keeps the fresh tool result intact (agent must see
/// the credentials exactly once) but rewrites prior-turn results to a stub
/// so the LLM provider's prompt cache never re-receives the token.
/// "Prior" is defined as "at least one assistant message follows the tool
/// message in the list" — stateless, no per-instance bookkeeping.
/// </summary>
public sealed class SensitiveToolHistoryScrubChatClientTests
{
    [Fact]
    public async Task ScrubsPriorTurnToolMessage_WhenAssistantFollows()
    {
        var captor = new CapturingInnerClient();
        var sut = new SensitiveToolHistoryScrubChatClient(captor);

        var messages = new List<ChatMessage>
        {
            User("install the deps"),
            AssistantCallingTool("call-1", SensitiveToolNames.GetArtifactCredentials, "{}"),
            ToolResult("call-1", "secret-token-aaa"),
            Assistant("I will now configure the feed."),
            AssistantCallingTool("call-2", "read_file", "{\"path\":\"NuGet.Config\"}"),
            ToolResult("call-2", "<config>raw file contents</config>"),
        };

        await sut.GetResponseAsync(messages);

        var seen = captor.LastMessages!;
        ToolResultText(seen, "call-1").Should().Be("[set, applied earlier turn]");
        // Non-sensitive call passes through untouched.
        ToolResultText(seen, "call-2").Should().Be("<config>raw file contents</config>");
    }

    [Fact]
    public async Task DoesNotScrub_WhenSensitiveToolResultIsTrailing()
    {
        var captor = new CapturingInnerClient();
        var sut = new SensitiveToolHistoryScrubChatClient(captor);

        var messages = new List<ChatMessage>
        {
            User("install the deps"),
            AssistantCallingTool("call-1", SensitiveToolNames.GetArtifactCredentials, "{}"),
            ToolResult("call-1", "secret-token-aaa"),
        };

        await sut.GetResponseAsync(messages);

        // Fresh tool result (no assistant message after it) must reach the
        // provider unchanged so the agent gets the credentials this turn.
        ToolResultText(captor.LastMessages!, "call-1").Should().Be("secret-token-aaa");
    }

    [Fact]
    public async Task LeavesNonSensitiveToolResultsAlone_AcrossMultipleTurns()
    {
        var captor = new CapturingInnerClient();
        var sut = new SensitiveToolHistoryScrubChatClient(captor);

        var messages = new List<ChatMessage>
        {
            User("read foo"),
            AssistantCallingTool("call-1", "read_file", "{\"path\":\"foo\"}"),
            ToolResult("call-1", "foo body"),
            Assistant("Got it."),
            AssistantCallingTool("call-2", "read_file", "{\"path\":\"bar\"}"),
            ToolResult("call-2", "bar body"),
        };

        await sut.GetResponseAsync(messages);

        ToolResultText(captor.LastMessages!, "call-1").Should().Be("foo body");
        ToolResultText(captor.LastMessages!, "call-2").Should().Be("bar body");
    }

    private static ChatMessage User(string text) =>
        new(ChatRole.User, [new TextContent(text)]);

    private static ChatMessage Assistant(string text) =>
        new(ChatRole.Assistant, [new TextContent(text)]);

    private static ChatMessage AssistantCallingTool(string callId, string name, string args) =>
        new(ChatRole.Assistant, [new FunctionCallContent(callId, name, ParseArgs(args))]);

    private static ChatMessage ToolResult(string callId, string content) =>
        new(ChatRole.Tool, [new FunctionResultContent(callId, content)]);

    private static IDictionary<string, object?>? ParseArgs(string json) =>
        string.IsNullOrEmpty(json) || json == "{}"
            ? new Dictionary<string, object?>()
            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(json);

    private static string? ToolResultText(IList<ChatMessage> messages, string callId)
    {
        foreach (var msg in messages)
        {
            foreach (var part in msg.Contents)
            {
                if (part is FunctionResultContent r && r.CallId == callId)
                    return r.Result?.ToString();
            }
        }
        return null;
    }

    private sealed class CapturingInnerClient : IChatClient
    {
        public IList<ChatMessage>? LastMessages { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastMessages = messages.ToList();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [new TextContent("ok")])));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
