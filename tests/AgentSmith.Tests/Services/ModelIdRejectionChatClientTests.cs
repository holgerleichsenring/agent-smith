using System.ClientModel;
using System.Net;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-10-12-6bfae: the Anthropic API answered 404 "model: anthropic.claude-sonnet-5-5" — a
/// Bedrock spelling picked for an Anthropic agent — and that bare text was the whole run summary.
/// </summary>
public sealed class ModelIdRejectionChatClientTests
{
    private const string BedrockId = "anthropic.claude-sonnet-5-5";

    [Fact]
    public async Task ModelIdRejection_AnthropicNotFound_NamesRoleIdAndConfig()
    {
        var agent = AgentUsing("sonnet", BedrockId);
        var client = Wrap(new HttpRequestException($"model: {BedrockId}", null, HttpStatusCode.NotFound), agent, TaskType.Planning);

        var thrown = await Call(client).Should().ThrowAsync<ModelIdRejectedException>();

        thrown.Which.Message.Should().Contain($"model id '{BedrockId}'").And.Contain("'planning' model role")
            .And.Contain("catalog entry 'sonnet'").And.Contain("Config Studio").And.Contain("anthropic provider");
        thrown.Which.Role.Should().Be("planning");
        thrown.Which.ModelId.Should().Be(BedrockId);
        thrown.Which.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task ModelIdRejection_ClientResult404_IsRecognised()
    {
        var agent = new AgentConfig { Type = "openai", Model = "gpt-9" };
        var client = Wrap(new ClientResultException("model_not_found", new FakeResponse(404)), agent, TaskType.Primary);

        (await Call(client).Should().ThrowAsync<ModelIdRejectedException>())
            .Which.Message.Should().Contain("model id 'gpt-9'").And.Contain("'primary' model role");
    }

    [Fact]
    public async Task ModelIdRejection_OtherFailure_PassesThrough()
    {
        var agent = new AgentConfig { Type = "openai", Model = "gpt-9" };
        var client = Wrap(new ClientResultException("bad request", new FakeResponse(400)), agent, TaskType.Primary);

        await Call(client).Should().ThrowAsync<ClientResultException>();
    }

    [Fact]
    public void IsNotFound_WrappedDeeper_IsStillRecognised() =>
        ModelIdRejectionChatClient.IsNotFound(new InvalidOperationException(
                "call failed", new HttpRequestException("x", null, HttpStatusCode.NotFound)))
            .Should().BeTrue();

    [Fact]
    public void Explain_AzureOpenAi_LeadsWithTheDeployment() =>
        ModelIdRejectionChatClient.Explain("primary", null, "gpt-4.1", "prod-41", "azure_openai")
            .Should().StartWith("The azure_openai provider rejected the deployment 'prod-41'")
            .And.Contain("deployment must exist");

    [Fact]
    public void HumanizeRunSummary_ModelIdRejection_PassesThrough()
    {
        var message = ModelIdRejectionChatClient.Explain("planning", "sonnet", BedrockId, null, "anthropic");

        ErrorFormatter.HumanizeRunSummary(message).Should().Be(message);
    }

    private static AgentConfig AgentUsing(string entry, string model) => new()
    {
        Type = "anthropic",
        Catalog = { [entry] = new CatalogModel { Model = model } },
        Models = new ModelRegistryConfig { Planning = new ModelAssignment { Use = entry } },
    };

    private static IChatClient Wrap(Exception failure, AgentConfig agent, TaskType task) =>
        ModelIdRejectionChatClient.Wrap(
            new ThrowingChat(failure), agent, task, new ModelRoleChain(agent).For(task), agent.Type);

    private static Func<Task<ChatResponse>> Call(IChatClient client) =>
        () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options: null, CancellationToken.None);

    private sealed class ThrowingChat(Exception failure) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw failure;

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
