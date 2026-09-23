using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
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
/// 2026-09-23-cd28: a bootstrap round is dispatched for ONE discovered component, and
/// <see cref="ContextNameGuard"/> already states what may be written — but the bootstrap
/// path built it with no discovered contexts, and with none it admits any name. A round
/// could therefore author <c>.agentsmith/contexts/&lt;invented&gt;/context.yaml</c> beside
/// the one it was asked for; nothing deletes a context directory, and BootstrapCheck then
/// probes both. These tests run the real round and read what the tool answered.
/// </summary>
public sealed class BootstrapContextNameGuardTests
{
    private const string RepoName = "monorepo";
    private const string DispatchedContext = "server";
    private const string ExistingOnDisk = "meta:\n  workdir: server\n";
    private const string GuardMarker = "is not a discovered context";

    private static readonly RoleSkillDefinition BootstrapSkill = new()
    {
        Name = "project-bootstrap",
        DisplayName = "Project Bootstrap",
        Description = "test",
        Emoji = "🔧",
        Rules = "test",
        Role = "producer",
        OutputSchema = "bootstrap",
    };

    [Fact]
    public async Task Bootstrap_AnInventedNameWithOneCandidate_IsRedirected()
    {
        var (_, written) = await RunAsync(named: "default", discovered: [DispatchedContext]);

        written.Should().Contain(".agentsmith/contexts/server/context.yaml",
            "one real context is an unambiguous target, so the invented name is redirected to it");
        written.Should().NotContain("contexts/default/",
            "a stray sibling directory is exactly what this round must not author");
    }

    [Fact]
    public async Task Bootstrap_AnInventedNameWithSeveralCandidates_IsRefused()
    {
        var (result, refusal) = await RunAsync(
            named: "default", discovered: [DispatchedContext, "client"]);

        refusal.Should().Contain(GuardMarker)
            .And.Contain("server").And.Contain("client",
                "the refusal names the contexts discovery actually resolved");
        refusal.Should().NotContain(SandboxContextYamlWriter.WrittenPrefix,
            "several candidates cannot be chosen between, so nothing is written");
        result.IsSuccess.Should().BeFalse("the round produced no context.yaml");
        result.Message.Should().Contain(GuardMarker, "the round carries the tool's refusal out");
    }

    [Fact]
    public async Task Bootstrap_ADiscoveredName_IsWrittenUnchanged()
    {
        var (result, written) = await RunAsync(
            named: DispatchedContext, discovered: [DispatchedContext, "client"]);

        written.Should().StartWith(SandboxContextYamlWriter.WrittenPrefix)
            .And.Contain(".agentsmith/contexts/server/context.yaml",
                "a name discovery resolved is written where it says, neither redirected nor refused");
        written.Should().NotContain(GuardMarker);
        result.IsSuccess.Should().BeTrue(result.Message);
    }

    private static async Task<(CommandResult Result, string? ToolAnswer)> RunAsync(
        string named, IReadOnlyList<string> discovered)
    {
        var chat = new NamingChatClient(named);
        var handler = NewHandler(chat);
        var result = await handler.ExecuteAsync(
            new BootstrapRoundContext(
                BootstrapSkill.Name, RepoName, new AgentConfig(),
                NewPipeline(new StubSandbox(), discovered),
                ContextName: DispatchedContext, Workdir: "server"),
            CancellationToken.None);
        return (result, chat.ToolAnswer);
    }

    private static BootstrapRoundHandler NewHandler(IChatClient chat) => new(
        new StubChatClientFactory(chat),
        new BootstrapToolHostFactory(
            Mock.Of<IDecisionLogger>(), new SandboxFileReaderFactory(),
            new PathReadGuard(new NullGitIgnoreResolver()),
            new PathWriteGuard(new PathReadGuard(new NullGitIgnoreResolver())),
            ContextGates.Serializer(),
            ContextGates.Build(), ContextGates.Writer(), ContextGates.DerivationStamp()),
        BootstrapReaderStubs.MetaFilesReturning(contextYaml: ExistingOnDisk, principles: null),
        PrinciplesTransferStubs.Composing("# Coding Principles\ncore + delta\n"),
        new BootstrapContextWriteVerdict(),
        new BootstrapOutputRecorder(),
        new SandboxTargets(),
        EventTestStubs.RunContext,
        NullLogger<BootstrapRoundHandler>.Instance);

    private static PipelineContext NewPipeline(ISandbox sandbox, IReadOnlyList<string> discovered)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            "init-project", new AgentConfig(), "skills", null));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [RepoName] = sandbox });
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal) { [RepoName] = NewMap() });
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, new[] { BootstrapSkill });
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("main"), "https://x/y.git"));
        // What BootstrapDiscover published and BootstrapDispatch fanned this round out of.
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents,
            new Dictionary<string, IReadOnlyList<DiscoveredComponent>>(StringComparer.Ordinal)
            {
                [RepoName] = discovered
                    .Select(name => new DiscoveredComponent(name, name, "csharp", $"{name}/Program.cs"))
                    .ToList(),
            });
        return pipeline;
    }

    private static ProjectMap NewMap() =>
        new(PrimaryLanguage: "csharp",
            Frameworks: [],
            Modules: [],
            TestProjects: [],
            EntryPoints: [],
            Conventions: new Conventions(NamingPattern: null, TestLayout: null, ErrorHandling: null),
            Ci: new CiConfig(HasCi: false, BuildCommand: null, TestCommand: null, CiSystem: null));

    // The producer skill, calling write_context_yaml under the name the test gives it.
    private sealed class NamingChatClient(string contextName) : IChatClient
    {
        /// <summary>What write_context_yaml answered — a written path, or the guard's refusal.</summary>
        public string? ToolAnswer { get; private set; }

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ToolAnswer = await BootstrapToolCall.WriteContextYamlAsync(
                options, BootstrapToolCall.ValidDocument, contextName);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class StubChatClientFactory(IChatClient client) : IChatClientFactory
    {
        public IChatClient Create(
            AgentConfig agent, TaskType task, int? maxIterations = null,
            MasterLoopHooks? masterLoopHooks = null) => client;

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 8192;
        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";
    }
}
