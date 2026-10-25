using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Factories;

/// <summary>2026-10-25-c5a7i: no secret value the server knows reaches the model provider.</summary>
public sealed class SecretValueMaskingChatClientTests
{
    internal const string Token = "npm_registry_token_0123456789";

    [Fact]
    public async Task SecretValueMaskingChatClient_ToolResultCarriesARegistryToken_ProviderSeesPlaceholder()
    {
        var provider = new CapturingChatClient();
        var call = new FunctionCallContent("c1", "read_file");
        ChatMessage[] messages =
        [
            new(ChatRole.Assistant, [call]),
            new(ChatRole.Tool, [new FunctionResultContent("c1", $"//registry/:_authToken={Token}")]),
        ];

        await Masking(provider).GetResponseAsync(messages);

        var sent = provider.Sent.Last().Contents.OfType<FunctionResultContent>().Single();
        sent.Result.Should().Be($"//registry/:_authToken={SecretMasker.ProviderPlaceholder}");
    }

    [Fact]
    public async Task SecretValueMaskingChatClient_UserText_IsMasked()
    {
        var provider = new CapturingChatClient();

        await Masking(provider).GetResponseAsync([new ChatMessage(ChatRole.User, $"use {Token} please")]);

        provider.Sent.Single().Text.Should().Be($"use {SecretMasker.ProviderPlaceholder} please");
    }

    [Fact]
    public async Task SecretValueMaskingChatClient_FreshSensitiveToolResult_PassesUnmasked()
    {
        var provider = new CapturingChatClient();
        ChatMessage[] messages =
        [
            new(ChatRole.Assistant, [new FunctionCallContent("c1", SensitiveToolNames.GetArtifactCredentials)]),
            new(ChatRole.Tool, [new FunctionResultContent("c1", Token)]),
        ];

        await Masking(provider).GetResponseAsync(messages);

        provider.Sent.Last().Contents.OfType<FunctionResultContent>().Single().Result.Should().Be(Token);
    }

    [Fact]
    public async Task SecretValueMaskingChatClient_CallerMessageList_IsNotMutated()
    {
        var provider = new CapturingChatClient();
        var original = new ChatMessage(ChatRole.User, $"token {Token}");
        var untouched = new ChatMessage(ChatRole.User, "nothing secret");
        var messages = new List<ChatMessage> { original, untouched };

        await Masking(provider).GetResponseAsync(messages);

        messages.Should().HaveCount(2);
        messages[0].Text.Should().Contain(Token);
        provider.Sent[1].Should().BeSameAs(untouched, "a message with nothing to mask keeps its identity");
    }

    internal static SecretMasker Masker() =>
        new(new LoadedSecretValues(new AgentSmithConfig { Secrets = new Dictionary<string, string> { ["npm"] = Token } }));

    private static IChatClient Masking(IChatClient provider) => new SecretValueMaskingChatClient(provider, Masker());

    internal sealed class CapturingChatClient : IChatClient
    {
        public List<ChatMessage> Sent { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Sent.Clear();
            Sent.AddRange(messages);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
