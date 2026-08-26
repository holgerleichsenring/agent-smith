using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
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
/// 2026-08-26-167c: a round must know whether it WROTE, not whether a file EXISTS.
/// <para>
/// BootstrapRoundHandler asked the sandbox whether context.yaml is there. On a first
/// init that is the same question. On a RE-INIT — the supported route back in — the
/// file is already on disk from last time, so every refusal still resolved to success:
/// the round reported green and the stale context survived untouched.
/// </para>
/// </summary>
public sealed class BootstrapContextWriteTests
{
    private const string ExistingOnDisk = "meta:\n  workdir: server\n";

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
    public async Task Bootstrap_AReinitWhoseWriteWasRefused_FailsRatherThanReportingSuccess()
    {
        var result = await RunAsync(BootstrapToolCall.RefusedDocument);

        result.IsSuccess.Should().BeFalse(
            "the file on disk is LAST init's — a green round here means the operator is "
            + "told the context was refreshed when it was not");
        result.Message.Should().Contain("REFUSED")
            .And.Contain("previous one, untouched");
    }

    [Fact]
    public async Task Bootstrap_TheWriteWasRefused_NamesTheRefusalAndItsDefect()
    {
        var result = await RunAsync(BootstrapToolCall.RefusedDocument);

        result.Message.Should().Contain("write_context_yaml")
            .And.Contain("/stack/image", "the round carries the tool's own defect out")
            .And.Contain("stack.image is required");
    }

    [Fact]
    public async Task Bootstrap_TheToolWasNeverCalled_StillSaysThat()
    {
        var result = await RunAsync(document: null);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("was not called",
            "never calling the tool and being refused by it are different failures");
        result.Message.Should().NotContain("REFUSED");
    }

    [Fact]
    public async Task Bootstrap_AWriteThisRoundSucceeded_IsAGreenRound()
    {
        var result = await RunAsync(
            BootstrapToolCall.ValidDocument,
            PrinciplesTransferStubs.Composing("# Coding Principles\ncore + delta\n"));

        result.IsSuccess.Should().BeTrue(result.Message);
    }

    private static async Task<CommandResult> RunAsync(
        string? document, BootstrapPrinciplesTransfer? transfer = null)
    {
        var sandbox = new StubSandbox();
        var handler = NewHandler(document, transfer ?? PrinciplesTransferStubs.NoTemplates());
        return await handler.ExecuteAsync(
            new BootstrapRoundContext(
                BootstrapSkill.Name, "monorepo", new AgentConfig(), NewPipeline(sandbox),
                ContextName: "server", Workdir: "server"),
            CancellationToken.None);
    }

    private static BootstrapRoundHandler NewHandler(
        string? document, BootstrapPrinciplesTransfer transfer) => new(
        new StubChatClientFactory(new WritingChatClient(document)),
        new BootstrapToolHostFactory(
            Mock.Of<IDecisionLogger>(),
            new PathReadGuard(new NullGitIgnoreResolver()),
            new PathWriteGuard(new PathReadGuard(new NullGitIgnoreResolver())),
            Mock.Of<IContextYamlSerializer>(),
            ContextGates.Build()),
        // The re-init shape: a context.yaml is already on the sandbox before the round.
        BootstrapReaderStubs.ReaderFactoryReturning(
            contextYaml: ExistingOnDisk, codingPrinciples: null),
        transfer,
        new BootstrapContextWriteVerdict(),
        new BootstrapOutputRecorder(),
        EventTestStubs.RunContext,
        NullLogger<BootstrapRoundHandler>.Instance);

    private static PipelineContext NewPipeline(ISandbox sandbox)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            "init-project", new AgentConfig(), "skills", null));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["monorepo"] = sandbox });
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal) { ["monorepo"] = NewMap() });
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, new[] { BootstrapSkill });
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("main"), "https://x/y.git"));
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

    // Null document = a skill that answers in prose and calls nothing.
    private sealed class WritingChatClient(string? document) : IChatClient
    {
        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (document is not null)
                await BootstrapToolCall.WriteContextYamlAsync(options, document);
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
