using AgentSmith.Application.Models.Registry;
using AgentSmith.Application.Services.Registry;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Registry;

/// <summary>
/// p0375: the hard contract for generic registry-auth staging — the real token
/// is substituted HOST-SIDE and never enters the LLM prompt, response, or
/// history; the path allowlist rejects everything outside the sandbox-home
/// config scope BEFORE any write; a leaked secret or an unmatched placeholder
/// is rejected loudly, not written; detection is a verbatim host-grep with no
/// manifest parsing.
/// </summary>
public sealed class RegistryAuthStagingTests
{
    private const string Host = "registry.widget.example";
    private const string Token = "SUPER-SECRET-TOKEN-42";

    private static readonly IReadOnlyList<RegistryConfig> Registries =
        new[] { new RegistryConfig(Host, "any", Token) };

    [Fact]
    public void TokenSubstitutor_PlaceholderForKnownHost_ReplacedWithMatchedToken()
    {
        var content = $"token = \"{RegistryTokenPlaceholder.For(Host)}\"";

        var result = new RegistryTokenSubstitutor().Substitute(content, Registries);

        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Contain(Token);
        result.Content.Should().NotContain(RegistryTokenPlaceholder.For(Host));
    }

    [Fact]
    public void TokenSubstitutor_UnmatchedPlaceholder_FailsLoudly_NoEmptyWrite()
    {
        var content = $"token = \"{RegistryTokenPlaceholder.For("unknown.host.example")}\"";

        var result = new RegistryTokenSubstitutor().Substitute(content, Registries);

        result.IsSuccess.Should().BeFalse();
        result.Content.Should().BeNull("a half-substituted or empty auth file silently breaks restore");
        result.FailureReason.Should().Contain("unknown.host.example");
    }

    [Theory]
    [InlineData("/etc/profile.d/auth.sh", "system path")]
    [InlineData("/usr/local/share/auth.toml", "system path")]
    [InlineData("/work/.cargo/config.toml", "repo working-tree path")]
    [InlineData("/root/../etc/passwd", "traversal")]
    [InlineData("~/../etc/passwd", "traversal via home")]
    [InlineData(".config/widget/auth.toml", "relative path")]
    [InlineData("/root/plain-file.toml", "not a dotfile/config scope under home")]
    public void PathGuard_OutsideUserConfigScope_TraversalOrRepoPath_RejectedBeforeWrite(
        string path, string because)
    {
        var verdict = new RegistryAuthPathGuard().Check(path);

        verdict.IsAllowed.Should().BeFalse(because);
        verdict.NormalizedPath.Should().BeNull();
        verdict.Reason.Should().NotBeNullOrEmpty("every rejection feeds a loud decision line");
    }

    [Theory]
    [InlineData("/root/.cargo/credentials.toml", "/root/.cargo/credentials.toml")]
    [InlineData("/root/.netrc", "/root/.netrc")]
    [InlineData("~/.config/widget/auth.toml", "/root/.config/widget/auth.toml")]
    public void PathGuard_SandboxHomeConfigScope_AllowedAndNormalized(string path, string expected)
    {
        var verdict = new RegistryAuthPathGuard().Check(path);

        verdict.IsAllowed.Should().BeTrue();
        verdict.NormalizedPath.Should().Be(expected);
    }

    [Fact]
    public void SecretLeakGuard_OutputContainingRealTokenNotPlaceholder_Rejected()
    {
        var guard = new SecretLeakGuard();

        guard.IsClean($"token = \"{Token}\"", Registries).Should()
            .BeFalse("the model echoed a real secret it read out of the repo");
        guard.LeakedHosts($"token = \"{Token}\"", Registries).Should()
            .ContainSingle().Which.Should().Be(Host);
        guard.IsClean($"token = \"{RegistryTokenPlaceholder.For(Host)}\"", Registries).Should()
            .BeTrue("placeholder-only output is clean");
    }

    [Fact]
    public async Task RegistryAuthStager_PromptAndResponse_ContainNoRealToken()
    {
        var capturing = new CapturingChatClientFactory(
            $$"""{"files":[{"path":"/root/.config/widget/auth.toml","content":"token=\"{{RegistryTokenPlaceholder.For(Host)}}\""}]}""");
        var stager = new RegistryAuthStager(
            capturing, new StagedAuthFileJsonReader(), Mock.Of<IRunContextAccessor>(),
            new LoopLimitsConfig(), NullLogger<RegistryAuthStager>.Instance);
        var uncovered = new[]
        {
            new UncoveredRegistry(new RegistryConfig(Host, "any", Token), new[] { "/work/widget.manifest" }),
        };

        var result = await stager.StageAsync(
            Mock.Of<ISandbox>(), "/work", uncovered, new AgentConfig(), CancellationToken.None);

        capturing.SentPromptText.Should().NotContain(Token, "the token must never enter the LLM prompt");
        capturing.SentPromptText.Should().Contain(Host, "the LLM is told the host");
        capturing.SentPromptText.Should().Contain("/work/widget.manifest",
            "the host-grep's matching paths ride along as context");
        capturing.SentPromptText.Should().Contain(RegistryTokenPlaceholder.Prefix,
            "the LLM is told the placeholder convention");
        result.Files.Should().ContainSingle();
        result.Files[0].Content.Should().NotContain(Token, "the response carries the placeholder, not the token");
        result.Files[0].Content.Should().Contain(RegistryTokenPlaceholder.For(Host));
        result.TargetedHosts.Should().Contain(Host);
    }

    [Fact]
    public async Task DetectUncovered_HostGrep_NoManifestParsing_MatchedPathsCollected()
    {
        // An UNKNOWN, never-parsed file format: detection must key on the verbatim
        // host string alone, collecting every matching path as LLM context.
        var reader = new Mock<ISandboxFileReader>();
        var listing = new[]
        {
            "/work/widget.manifest", "/work/docs/notes.txt", "/work/logo.png", "/work/nuget.config",
        };
        reader.Setup(r => r.TryReadAsync("/work/widget.manifest", It.IsAny<CancellationToken>()))
            .ReturnsAsync($"registry \"{Host}\" fetch-with quantum-protocol v9");
        reader.Setup(r => r.TryReadAsync("/work/docs/notes.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync($"packages come from {Host} — ask ops for access");
        var grep = new RegistryHostGrep(NullLogger<RegistryHostGrep>.Instance);

        var uncovered = await grep.FindUncoveredAsync(
            listing, new HashSet<string>(StringComparer.OrdinalIgnoreCase), Registries,
            reader.Object, "repo", CancellationToken.None);

        var single = uncovered.Should().ContainSingle().Subject;
        single.Registry.Host.Should().Be(Host);
        single.MatchingPaths.Should().Equal("/work/widget.manifest", "/work/docs/notes.txt");
        reader.Verify(r => r.TryReadAsync("/work/logo.png", It.IsAny<CancellationToken>()),
            Times.Never, "binary files are skipped");
        reader.Verify(r => r.TryReadAsync("/work/nuget.config", It.IsAny<CancellationToken>()),
            Times.Never, "fast-path files are the deterministic path's domain");
    }

    [Fact]
    public async Task DetectUncovered_CoveredOrTokenlessHost_NotScanned()
    {
        var reader = new Mock<ISandboxFileReader>();
        var registries = new[]
        {
            new RegistryConfig(Host, "any", Token),
            new RegistryConfig("tokenless.example", "any", string.Empty),
        };
        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Host };
        var grep = new RegistryHostGrep(NullLogger<RegistryHostGrep>.Instance);

        var uncovered = await grep.FindUncoveredAsync(
            new[] { "/work/widget.manifest" }, covered, registries,
            reader.Object, "repo", CancellationToken.None);

        uncovered.Should().BeEmpty();
        reader.Verify(r => r.TryReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never, "no candidates means no file reads at all — the fast-path repos never pay for the grep");
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
