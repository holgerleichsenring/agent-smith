using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Factories;

/// <summary>
/// p0374: history caching for the Claude path. Unit-level tests pin the wire
/// body transform (<see cref="ClaudeHistoryCacheHandler.TryMarkLastMessage"/>);
/// the wire tests drive it end-to-end through the builder's test-transport seam
/// and confirm the OTHER providers are untouched.
/// </summary>
public sealed class ClaudeHistoryCacheTests : IDisposable
{
    private const string KeySecret = "AS_HISTCACHE_KEY";

    public ClaudeHistoryCacheTests() => Environment.SetEnvironmentVariable(KeySecret, "test-key");
    public void Dispose() => Environment.SetEnvironmentVariable(KeySecret, null);

    // ── the wire transform in isolation ─────────────────────────────────────
    [Fact]
    public void TryMarkLastMessage_MultiTurn_MarksLastBlockOfLastMessage()
    {
        var body = """
            {"model":"claude-sonnet-5","messages":[
              {"role":"user","content":[{"type":"text","text":"read file A"}]},
              {"role":"assistant","content":[{"type":"text","text":"contents A"}]},
              {"role":"user","content":[{"type":"text","text":"now migrate"}]}
            ],"system":[{"type":"text","text":"master","cache_control":{"type":"ephemeral"}}]}
            """;

        ClaudeHistoryCacheHandler.TryMarkLastMessage(body, out var patched).Should().BeTrue();

        // The last message's block is now marked; earlier messages are not.
        patched.Should().Contain("\"now migrate\",\"cache_control\":{\"type\":\"ephemeral\"}");
        Regex.Matches(patched, "cache_control").Count.Should().Be(2, "system (pre-marked) + the last message");
    }

    [Fact]
    public void TryMarkLastMessage_AlreadyMarked_IsIdempotent()
    {
        var body = """
            {"messages":[{"role":"user","content":[{"type":"text","text":"q","cache_control":{"type":"ephemeral"}}]}]}
            """;

        ClaudeHistoryCacheHandler.TryMarkLastMessage(body, out _).Should().BeFalse(
            "the last block already carries a breakpoint — nothing to add");
    }

    [Fact]
    public void TryMarkLastMessage_FourBreakpointsAlready_SkipsToRespectAnthropicLimit()
    {
        // system + 3 tools already at the 4-breakpoint ceiling — adding a 5th is illegal.
        var body = """
            {"messages":[{"role":"user","content":[{"type":"text","text":"q"}]}],
             "system":[{"type":"text","text":"s","cache_control":{"type":"ephemeral"}}],
             "tools":[{"name":"a","cache_control":{"type":"ephemeral"}},
                      {"name":"b","cache_control":{"type":"ephemeral"}},
                      {"name":"c","cache_control":{"type":"ephemeral"}}]}
            """;

        ClaudeHistoryCacheHandler.TryMarkLastMessage(body, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{}")]
    [InlineData("""{"messages":[]}""")]
    [InlineData("""{"messages":[{"role":"user","content":"a plain string"}]}""")]
    public void TryMarkLastMessage_UnrecognisedShape_LeavesBodyUntouched(string body)
    {
        ClaudeHistoryCacheHandler.TryMarkLastMessage(body, out var patched).Should().BeFalse();
        patched.Should().Be(body, "a caching optimisation must never corrupt the request");
    }

    // ── end-to-end through the builder ──────────────────────────────────────
    [Fact]
    public async Task ClaudeCache_MultiTurnReadHeavy_LastMessageCarriesCacheControl()
    {
        var handler = new RecordingHandler(AnthropicResponse());
        var client = new ClaudeChatClientBuilder(handler).Build(
            new AgentConfig { Type = "claude", ApiKeySecret = KeySecret },
            new ModelAssignment { Model = "claude-sonnet-5" });

        await client.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, "You are the coding master."),
                new ChatMessage(ChatRole.User, "read file A"),
                new ChatMessage(ChatRole.Assistant, "contents of file A"),
                new ChatMessage(ChatRole.User, "now do the migration"),
            ],
            new ChatOptions());

        // The accumulated history (last message) is now cached, not just system.
        handler.LastBody.Should().Contain("now do the migration")
            .And.MatchRegex("\"now do the migration\"[^]]*cache_control",
                "the last conversation message must carry the ephemeral breakpoint");
    }

    [Fact]
    public async Task ClaudeCache_Disabled_NoMessageMarkerStamped()
    {
        var handler = new RecordingHandler(AnthropicResponse());
        var agent = new AgentConfig { Type = "claude", ApiKeySecret = KeySecret, Cache = new CacheConfig { IsEnabled = false } };
        var client = new ClaudeChatClientBuilder(handler).Build(agent, new ModelAssignment { Model = "claude-sonnet-5" });

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "just one turn")], new ChatOptions());

        handler.LastBody.Should().NotContain("cache_control",
            "IsEnabled=false disables both the automatic system marker and the history handler");
    }

    [Fact]
    public async Task ClaudeCache_BreakpointCount_StaysWithinAnthropicLimitOfFour()
    {
        var handler = new RecordingHandler(AnthropicResponse());
        var client = new ClaudeChatClientBuilder(handler).Build(
            new AgentConfig { Type = "claude", ApiKeySecret = KeySecret },
            new ModelAssignment { Model = "claude-sonnet-5" });

        await client.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, "master"),
                new ChatMessage(ChatRole.User, "read"),
                new ChatMessage(ChatRole.User, "migrate"),
            ],
            new ChatOptions
            {
                Tools = [AIFunctionFactory.Create((string p) => "ok", "read_file", "Read a file")],
            });

        Regex.Matches(handler.LastBody, "cache_control").Count.Should().BeLessThanOrEqualTo(4);
    }

    [Fact]
    public async Task OpenAiPath_MessageHistoryCaching_UnaffectedByTheChange()
    {
        var handler = new RecordingHandler(OpenAiResponse());
        var client = new OpenAiChatClientBuilder(handler).Build(
            new AgentConfig { Type = "openai", ApiKeySecret = KeySecret },
            new ModelAssignment { Model = "gpt-4.1" });

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")], new ChatOptions());

        handler.LastBody.Should().NotContain("cache_control",
            "the Claude history handler is Anthropic-specific; OpenAI/Azure auto-cache prefixes with no markers");
    }

    private static string AnthropicResponse() => """
        {"id":"msg_test","type":"message","role":"assistant","model":"claude-sonnet-5",
         "content":[{"type":"text","text":"ok"}],"stop_reason":"end_turn","usage":{"input_tokens":10,"output_tokens":5}}
        """;

    private static string OpenAiResponse() => """
        {"id":"chatcmpl-test","object":"chat.completion","created":1700000000,"model":"gpt-4.1",
         "choices":[{"index":0,"message":{"role":"assistant","content":"hi back"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}
        """;

    private sealed class RecordingHandler(string responseJson) : HttpMessageHandler
    {
        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }
}
