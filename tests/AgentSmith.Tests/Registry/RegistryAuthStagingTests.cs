using AgentSmith.Application.Models.Registry;
using AgentSmith.Application.Services.Registry;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Events;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Registry;

/// <summary>
/// p0375: the hard contract for the generic registry-auth fallback — the real
/// token is substituted HOST-SIDE and never enters the LLM prompt, response, or
/// history; a leaked secret or an unmatched placeholder is rejected, not written.
/// </summary>
public sealed class RegistryAuthStagingTests
{
    private const string Host = "registry.widget.example";
    private const string Token = "SUPER-SECRET-TOKEN-42";

    [Fact]
    public void TokenSubstitutor_PlaceholderForKnownHost_ReplacedWithMatchedToken()
    {
        var content = $"token = \"{RegistryTokenPlaceholder.For(Host)}\"";
        var registries = new[] { new RegistryConfig(Host, "any", Token) };

        var result = new RegistryTokenSubstitutor().Substitute(content, registries);

        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Contain(Token);
        result.Content.Should().NotContain(RegistryTokenPlaceholder.For(Host));
    }

    [Fact]
    public void TokenSubstitutor_UnmatchedPlaceholder_FailsRatherThanWritingEmpty()
    {
        var content = $"token = \"{RegistryTokenPlaceholder.For("unknown.host.example")}\"";
        var registries = new[] { new RegistryConfig(Host, "any", Token) };

        var result = new RegistryTokenSubstitutor().Substitute(content, registries);

        result.IsSuccess.Should().BeFalse();
        result.Content.Should().BeNull("a half-substituted or empty auth file silently breaks restore");
        result.FailureReason.Should().Contain("unknown.host.example");
    }

    [Fact]
    public void SecretLeakGuard_OutputContainingRealTokenNotPlaceholder_Rejected()
    {
        var registries = new[] { new RegistryConfig(Host, "any", Token) };
        var guard = new SecretLeakGuard();

        guard.IsClean($"token = \"{Token}\"", registries).Should()
            .BeFalse("the model echoed a real secret it read out of the repo");
        guard.IsClean($"token = \"{RegistryTokenPlaceholder.For(Host)}\"", registries).Should()
            .BeTrue("placeholder-only output is clean");
    }

    [Fact]
    public async Task RegistryAuthStager_PromptAndResponse_ContainNoRealToken()
    {
        var capturing = new CapturingChatClientFactory(
            $$"""{"files":[{"path":"/root/.config/widget/auth.toml","content":"token=\"{{RegistryTokenPlaceholder.For(Host)}}\""}]}""");
        var stager = new RegistryAuthStager(
            capturing, new StagedAuthFileJsonReader(),
            Mock.Of<IRunContextAccessor>(), NullLogger<RegistryAuthStager>.Instance);
        var uncovered = new[]
        {
            new UncoveredRegistry(new RegistryConfig(Host, "any", Token), "/work/widget.manifest"),
        };

        var result = await stager.StageAsync(
            Mock.Of<ISandbox>(), "/work", uncovered, new AgentConfig(), CancellationToken.None);

        capturing.SentPromptText.Should().NotContain(Token, "the token must never enter the LLM prompt");
        capturing.SentPromptText.Should().Contain(Host, "the LLM is told the host");
        capturing.SentPromptText.Should().Contain(RegistryTokenPlaceholder.Prefix, "the LLM is told the placeholder convention");
        result.Files.Should().ContainSingle();
        result.Files[0].Content.Should().NotContain(Token, "the response carries the placeholder, not the token");
        result.Files[0].Content.Should().Contain(RegistryTokenPlaceholder.For(Host));
        result.TargetedHosts.Should().Contain(Host);
    }

    private sealed class CapturingChatClientFactory(string cannedResponse) : IChatClientFactory
    {
        public string SentPromptText { get; private set; } = string.Empty;

        public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null,
            MasterLoopHooks? masterLoopHooks = null) => new Inner(this, cannedResponse);

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;
        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";

        private sealed class Inner(CapturingChatClientFactory owner, string canned) : IChatClient
        {
            public Task<ChatResponse> GetResponseAsync(
                IEnumerable<ChatMessage> messages, ChatOptions? options = null,
                CancellationToken cancellationToken = default)
            {
                owner.SentPromptText = string.Join("\n", messages.Select(m => m.Text));
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, canned)));
            }

            public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
                IEnumerable<ChatMessage> messages, ChatOptions? options = null,
                CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public object? GetService(Type serviceType, object? serviceKey = null) => null;
            public void Dispose() { }
        }
    }
}
