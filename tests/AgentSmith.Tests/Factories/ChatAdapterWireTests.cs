using System.Net;
using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Factories;

/// <summary>
/// p0239c: wire-level tests for the OpenAI / Azure-OpenAI / Anthropic chat
/// adapters. They fake the HTTP transport ONE level below the SDK (the builder's
/// test-transport seam), so they exercise OUR request shaping + the SDK ↔
/// Microsoft.Extensions.AI response/tool-call parsing seam — the gap the
/// ScriptedChatClient (which scripts at IChatClient) cannot cover.
/// </summary>
public sealed class ChatAdapterWireTests : IDisposable
{
    private const string KeySecret = "AS_WIRETEST_KEY";

    public ChatAdapterWireTests() => Environment.SetEnvironmentVariable(KeySecret, "test-key");
    public void Dispose() => Environment.SetEnvironmentVariable(KeySecret, null);

    private static ChatOptions WithWeatherTool() => new()
    {
        Tools = [AIFunctionFactory.Create((string city) => "sunny", "get_weather", "Get the weather for a city")],
    };

    // ── OpenAI ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task OpenAi_RequestShaping_CarriesModelMessagesAndTools()
    {
        var handler = new RecordingHandler(OpenAiResponse());
        var client = new OpenAiChatClientBuilder(handler).Build(
            OpenAiAgent(), new ModelAssignment { Model = "gpt-4.1" });

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "weather in Berlin?")], WithWeatherTool());

        handler.LastUri.Should().Contain("chat/completions");
        handler.LastBody.Should().Contain("gpt-4.1", "the request must carry the model");
        handler.LastBody.Should().Contain("weather in Berlin?", "the user message must be in the request");
        handler.LastBody.Should().Contain("get_weather", "the tool definition must be in the request");
    }

    [Fact]
    public async Task OpenAi_ResponseWithToolCall_ParsesToFunctionCallContent()
    {
        var handler = new RecordingHandler(OpenAiResponse());
        var client = new OpenAiChatClientBuilder(handler).Build(
            OpenAiAgent(), new ModelAssignment { Model = "gpt-4.1" });

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "weather?")], WithWeatherTool());

        response.Text.Should().Contain("Hello from the fake");
        response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>()
            .Should().ContainSingle(c => c.Name == "get_weather");
    }

    [Fact]
    public void OpenAi_NetworkTimeout_DefaultsTo300_NotSdk100()
    {
        OpenAiChatClientBuilder.ResolveNetworkTimeout(new AgentConfig { NetworkTimeoutSeconds = 0 })
            .Should().Be(TimeSpan.FromSeconds(300), "0/unset must fall back to 300s, NOT the SDK's 100s default");
        OpenAiChatClientBuilder.ResolveNetworkTimeout(new AgentConfig { NetworkTimeoutSeconds = 120 })
            .Should().Be(TimeSpan.FromSeconds(120), "a configured value is honoured");
    }

    // ── Azure OpenAI ─────────────────────────────────────────────────────────
    [Fact]
    public async Task Azure_RequestShaping_RoutesViaDeployment()
    {
        var handler = new RecordingHandler(OpenAiResponse());
        var agent = new AgentConfig
        {
            Type = "azure_openai", ApiKeySecret = KeySecret,
            Endpoint = "https://test.openai.azure.com", Deployment = "my-deploy",
        };
        var client = new OpenAiChatClientBuilder(handler).Build(agent, new ModelAssignment { Model = "gpt-4.1" });

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions());

        handler.LastUri.Should().Contain("deployments/my-deploy", "Azure routes by deployment name in the URL");
        handler.LastUri.Should().Contain("test.openai.azure.com");
    }

    // ── Anthropic ────────────────────────────────────────────────────────────
    [Fact]
    public async Task Anthropic_RequestShaping_SetsModelIdAndMessages()
    {
        var handler = new RecordingHandler(AnthropicResponse());
        var client = new ClaudeChatClientBuilder(handler).Build(
            AnthropicAgent(), new ModelAssignment { Model = "claude-sonnet-4-6" });

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "weather in Berlin?")], new ChatOptions());

        handler.LastUri.Should().Contain("/v1/messages");
        handler.LastBody.Should().Contain("claude-sonnet-4-6", "the resolved model must be sent as `model`");
        handler.LastBody.Should().Contain("weather in Berlin?");
    }

    [Fact]
    public async Task Anthropic_ResponseWithToolUse_ParsesToFunctionCallContent()
    {
        var handler = new RecordingHandler(AnthropicResponse());
        var client = new ClaudeChatClientBuilder(handler).Build(
            AnthropicAgent(), new ModelAssignment { Model = "claude-sonnet-4-6" });

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "weather?")], WithWeatherTool());

        response.Text.Should().Contain("Hello from Claude");
        response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>()
            .Should().ContainSingle(c => c.Name == "get_weather");
    }

    // ── helpers ──────────────────────────────────────────────────────────────
    private static AgentConfig OpenAiAgent() => new() { Type = "openai", ApiKeySecret = KeySecret };
    private static AgentConfig AnthropicAgent() => new() { Type = "claude", ApiKeySecret = KeySecret };

    private static string OpenAiResponse() => """
        {"id":"chatcmpl-test","object":"chat.completion","created":1700000000,"model":"gpt-4.1",
         "choices":[{"index":0,"message":{"role":"assistant","content":"Hello from the fake",
         "tool_calls":[{"id":"call_1","type":"function","function":{"name":"get_weather","arguments":"{\"city\":\"Berlin\"}"}}]},
         "finish_reason":"tool_calls"}],
         "usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}
        """;

    private static string AnthropicResponse() => """
        {"id":"msg_test","type":"message","role":"assistant","model":"claude-sonnet-4-6",
         "content":[{"type":"text","text":"Hello from Claude"},
         {"type":"tool_use","id":"toolu_1","name":"get_weather","input":{"city":"Berlin"}}],
         "stop_reason":"tool_use","usage":{"input_tokens":10,"output_tokens":5}}
        """;

    private sealed class RecordingHandler(string responseJson) : HttpMessageHandler
    {
        public string? LastUri { get; private set; }
        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri?.ToString();
            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }
}
