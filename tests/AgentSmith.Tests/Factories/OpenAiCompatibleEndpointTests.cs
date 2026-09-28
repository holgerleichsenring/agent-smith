using System.Net;
using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Factories;

/// <summary>
/// type: openai with an endpoint is an OpenAI-COMPATIBLE server: requests go to that host,
/// and the key never falls back to OPENAI_API_KEY there, so an OpenAI key is never sent to a
/// third party. Observed on the wire through the builder's test transport.
/// </summary>
[Collection(nameof(OpenAiKeyEnvironment))]
public sealed class OpenAiCompatibleEndpointTests : IDisposable
{
    private const string NamedSecret = "AS_COMPAT_KEY";
    private const string OpenAiKey = "sk-real-openai-key";
    private readonly string? _savedOpenAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    public OpenAiCompatibleEndpointTests() => Environment.SetEnvironmentVariable("OPENAI_API_KEY", OpenAiKey);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", _savedOpenAiKey);
        Environment.SetEnvironmentVariable(NamedSecret, null);
    }

    [Fact]
    public async Task OpenAi_WithEndpoint_SendsToThatHost()
    {
        var handler = await Send(new AgentConfig { Type = "openai", Endpoint = "https://llm.internal.example/v1" });

        handler.LastUri.Should().StartWith("https://llm.internal.example/v1/chat/completions");
    }

    [Fact]
    public async Task OpenAi_WithoutEndpoint_SendsToApiOpenAiCom()
    {
        var handler = await Send(new AgentConfig { Type = "openai" });

        handler.LastUri.Should().StartWith("https://api.openai.com/");
        handler.LastAuthorization.Should().Be($"Bearer {OpenAiKey}");
    }

    [Fact]
    public void OpenAi_EndpointWithEmptyNamedSecret_ThrowsNamingTheVariable()
    {
        var agent = new AgentConfig
        {
            Type = "openai", Endpoint = "https://llm.internal.example/v1", ApiKeySecret = NamedSecret,
        };

        var build = () => new OpenAiChatClientBuilder(new RecordingHandler())
            .Build(agent, new ModelAssignment { Model = "m" });

        build.Should().Throw<InvalidOperationException>().WithMessage($"*'{NamedSecret}'*");
    }

    [Fact]
    public async Task OpenAi_EndpointWithoutSecret_NeverSendsOpenAiApiKey()
    {
        var handler = await Send(new AgentConfig { Type = "openai", Endpoint = "https://llm.internal.example/v1" });

        handler.LastAuthorization.Should().NotContain(OpenAiKey)
            .And.Be($"Bearer {OpenAiChatClientBuilder.UnauthenticatedKey}");
    }

    [Fact]
    public async Task OpenAi_EndpointWithNamedSecret_SendsThatKey()
    {
        Environment.SetEnvironmentVariable(NamedSecret, "compat-key");
        var handler = await Send(new AgentConfig
        {
            Type = "openai", Endpoint = "https://llm.internal.example/v1", ApiKeySecret = NamedSecret,
        });

        handler.LastAuthorization.Should().Be("Bearer compat-key");
    }

    [Fact]
    public async Task OpenAi_RoleEndpoint_WinsOverTheAgents()
    {
        var handler = new RecordingHandler();
        var client = new OpenAiChatClientBuilder(handler).Build(
            new AgentConfig { Type = "openai", Endpoint = "https://agent.example/v1" },
            new ModelAssignment { Model = "m", Endpoint = "https://role.example/v1" });

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        handler.LastUri.Should().StartWith("https://role.example/v1/");
    }

    [Fact]
    public void EffectiveEndpoint_RoleOnAnotherProvider_DoesNotInheritTheAgents()
    {
        var agent = new AgentConfig { Type = "ollama", Endpoint = "http://ollama:11434" };

        new ModelAssignment { Model = "m", ProviderType = "openai" }.EffectiveEndpoint(agent).Should().BeNull();
        new ModelAssignment { Model = "m" }.EffectiveEndpoint(agent).Should().Be("http://ollama:11434");
    }

    private static async Task<RecordingHandler> Send(AgentConfig agent)
    {
        var handler = new RecordingHandler();
        var client = new OpenAiChatClientBuilder(handler).Build(agent, new ModelAssignment { Model = "m" });
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);
        return handler;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private const string Response = """
            {"id":"c","object":"chat.completion","created":1,"model":"m",
             "choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],
             "usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}
            """;

        public string? LastUri { get; private set; }
        public string? LastAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri?.ToString();
            LastAuthorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Response, Encoding.UTF8, "application/json"),
            });
        }
    }
}
