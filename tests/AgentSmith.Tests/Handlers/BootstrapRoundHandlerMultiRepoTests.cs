using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0158g: BootstrapRoundHandler is dispatched once per repo with
/// <see cref="BootstrapRoundContext.RepoName"/>; it must resolve Sandboxes[X]
/// and RepoProjectMaps[X] (not the legacy singletons) so each round writes
/// into the correct per-repo sandbox with the correct per-repo ProjectMap.
/// Legacy single-repo fixtures continue to work via the empty-RepoName +
/// fallback path verified separately.
/// </summary>
public sealed class BootstrapRoundHandlerMultiRepoTests
{
    private static readonly RoleSkillDefinition CsharpSkill = new()
    {
        Name = "csharp-bootstrap",
        DisplayName = "C# Bootstrapper",
        Description = "test",
        Emoji = "🔧",
        Rules = "test",
    };

    [Fact]
    public async Task ExecuteAsync_MultiRepo_UsesRepoProjectMapForUserPrompt()
    {
        // Two repos with two distinct languages — RepoName="web" must drive the
        // handler to pick RepoProjectMaps["web"] (typescript), not the csharp
        // map under "api". The captured user-prompt asserts which ProjectMap
        // BootstrapPromptFactory.Build received.
        var captured = new CapturedPrompt();
        var handler = NewHandler(captured);
        var pipeline = NewPipeline();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal)
            {
                ["api"] = Mock.Of<ISandbox>(),
                ["web"] = Mock.Of<ISandbox>(),
            });
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal)
            {
                ["api"] = NewMap("csharp"),
                ["web"] = NewMap("typescript"),
            });
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, new[] { CsharpSkill });
        pipeline.Set(ContextKeys.Repository,
            new Repository(new BranchName("main"), "https://x/y.git"));

        await handler.ExecuteAsync(
            new BootstrapRoundContext(CsharpSkill.Name, "web", new AgentConfig(), pipeline),
            CancellationToken.None);

        captured.User.Should().Contain("\"PrimaryLanguage\": \"typescript\"");
        captured.User.Should().NotContain("\"PrimaryLanguage\": \"csharp\"");
    }

    [Fact]
    public async Task ExecuteAsync_RepoNameNotInSandboxes_FailsWithRepoNameInMessage()
    {
        var handler = NewHandler(new CapturedPrompt());
        var pipeline = NewPipeline();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal)
            {
                ["api"] = Mock.Of<ISandbox>(),
            });
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal)
            {
                ["api"] = NewMap("csharp"),
                ["docs"] = NewMap("markdown"),
            });
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, new[] { CsharpSkill });
        pipeline.Set(ContextKeys.Repository,
            new Repository(new BranchName("main"), "https://x/y.git"));

        var result = await handler.ExecuteAsync(
            new BootstrapRoundContext(CsharpSkill.Name, "docs", new AgentConfig(), pipeline),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("docs");
        result.Message.Should().Contain("no sandbox");
    }

    [Fact]
    public async Task ExecuteAsync_RepoNameNotInProjectMaps_FailsWithRepoNameInMessage()
    {
        var handler = NewHandler(new CapturedPrompt());
        var pipeline = NewPipeline();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal)
            {
                ["api"] = Mock.Of<ISandbox>(),
                ["docs"] = Mock.Of<ISandbox>(),
            });
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal)
            {
                ["api"] = NewMap("csharp"),
            });
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, new[] { CsharpSkill });
        pipeline.Set(ContextKeys.Repository,
            new Repository(new BranchName("main"), "https://x/y.git"));

        var result = await handler.ExecuteAsync(
            new BootstrapRoundContext(CsharpSkill.Name, "docs", new AgentConfig(), pipeline),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("docs");
        result.Message.Should().Contain("ProjectMap");
    }

    private static BootstrapRoundHandler NewHandler(CapturedPrompt captured) => new(
        new PromptCapturingFactory(new CapturingChatClient(captured)),
        new BootstrapToolHostFactory(Mock.Of<IDecisionLogger>(), new PathReadGuard(new NullGitIgnoreResolver()), new PathWriteGuard(new PathReadGuard(new NullGitIgnoreResolver()))),
        BootstrapReaderStubs.NullReaderFactory(),
        EventTestStubs.RunContext,
        NullLogger<BootstrapRoundHandler>.Instance);

    private static PipelineContext NewPipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            "init-project", new AgentConfig(), "skills/coding", null));
        return pipeline;
    }

    private static ProjectMap NewMap(string primaryLanguage) =>
        new(PrimaryLanguage: primaryLanguage,
            Frameworks: Array.Empty<string>(),
            Modules: Array.Empty<Module>(),
            TestProjects: Array.Empty<TestProject>(),
            EntryPoints: Array.Empty<string>(),
            Conventions: new Conventions(NamingPattern: null, TestLayout: null, ErrorHandling: null),
            Ci: new CiConfig(HasCi: false, BuildCommand: null, TestCommand: null, CiSystem: null));

    private sealed class CapturedPrompt
    {
        public string System { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
    }

    private sealed class CapturingChatClient(CapturedPrompt sink) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var list = messages.ToList();
            sink.System = list.FirstOrDefault(m => m.Role == ChatRole.System)?.Text ?? string.Empty;
            sink.User = list.FirstOrDefault(m => m.Role == ChatRole.User)?.Text ?? string.Empty;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class PromptCapturingFactory(IChatClient client) : IChatClientFactory
    {
        public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null) => client;
        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 8192;
        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";
    }
}
