using System.Net;
using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.ToolImages;

/// <summary>
/// 2026-10-01-283dd: what the real OpenAI and Anthropic adapters put on the wire for the
/// relay's shape — a user message carrying the image right after the tool message — and,
/// for OpenAI, why an image inside the tool message is no shape at all: it is dropped.
/// </summary>
public sealed class ToolImageWireShapeTests : IDisposable
{
    private const string KeySecret = "AS_TOOLIMAGE_WIRE_KEY";
    private static readonly string Base64 = Convert.ToBase64String(ToolImageLoopFixture.Png(10, 10));

    public ToolImageWireShapeTests() => Environment.SetEnvironmentVariable(KeySecret, "test-key");
    public void Dispose() => Environment.SetEnvironmentVariable(KeySecret, null);

    [Fact]
    public async Task OpenAi_ImageInUserMessageAfterTool_IsSentAsImageUrl()
    {
        var body = await Send(h => new OpenAiChatClientBuilder(h), "openai", OpenAiReply, ImageAfterTool());

        body.IndexOf("\"role\":\"tool\"", StringComparison.Ordinal).Should()
            .BeLessThan(body.LastIndexOf("\"role\":\"user\"", StringComparison.Ordinal));
        body.Should().Contain("image_url").And.Contain($"data:image/png;base64,{Base64}");
    }

    [Fact]
    public async Task OpenAi_ImageInsideToolMessage_IsDropped()
    {
        var body = await Send(h => new OpenAiChatClientBuilder(h), "openai", OpenAiReply, ImageInsideTool());

        body.Should().Contain("\"role\":\"tool\"").And.NotContain(Base64);
    }

    [Fact]
    public async Task Anthropic_ImageInUserMessageAfterTool_IsSentAsBase64ImageBlock()
    {
        var body = await Send(h => new ClaudeChatClientBuilder(h), "claude", AnthropicReply, ImageAfterTool());

        body.IndexOf("tool_result", StringComparison.Ordinal).Should()
            .BeLessThan(body.IndexOf("\"type\":\"image\"", StringComparison.Ordinal));
        body.Should().Contain("\"media_type\":\"image/png\"").And.Contain(Base64);
    }

    private static async Task<string> Send(
        Func<HttpMessageHandler, IChatClientBuilder> builder, string type, string reply, List<ChatMessage> messages)
    {
        var capture = new Capture(reply);
        var client = builder(capture).Build(
            new AgentConfig { Type = type, ApiKeySecret = KeySecret }, new ModelAssignment { Model = "m" });
        await client.GetResponseAsync(messages, new ChatOptions());
        return capture.Body;
    }

    private static List<ChatMessage> ImageAfterTool() =>
    [
        .. Prefix(),
        new(ChatRole.Tool, [new FunctionResultContent("call_1", "rendered")]),
        new(ChatRole.User, [new TextContent("[image from render, call call_1: page]"), Image()]),
    ];

    private static List<ChatMessage> ImageInsideTool() =>
        [.. Prefix(), new(ChatRole.Tool, [new FunctionResultContent("call_1", "rendered"), Image()])];

    private static List<ChatMessage> Prefix() =>
    [
        new(ChatRole.User, "render the page"),
        new(ChatRole.Assistant, [new FunctionCallContent("call_1", "render")]),
    ];

    private static DataContent Image() => new(ToolImageLoopFixture.Png(10, 10), "image/png");

    private const string OpenAiReply = """
        {"id":"x","object":"chat.completion","created":1,"model":"m","choices":[{"index":0,
         "message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}
        """;

    private const string AnthropicReply = """
        {"id":"x","type":"message","role":"assistant","model":"m","content":[{"type":"text","text":"ok"}],
         "stop_reason":"end_turn","usage":{"input_tokens":1,"output_tokens":1}}
        """;

    private sealed class Capture(string reply) : HttpMessageHandler
    {
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(reply, Encoding.UTF8, "application/json") };
        }
    }
}
