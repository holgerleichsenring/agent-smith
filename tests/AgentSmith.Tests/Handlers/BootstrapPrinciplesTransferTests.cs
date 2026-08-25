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
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0379: the bootstrap round TRANSFERS the authored core+delta composition
/// into coding-principles.md (framework write, before the skill call) instead
/// of LLM-generating principles from code archaeology; ratified principles
/// are never overwritten on re-init; pre-p0379 catalogs keep the legacy
/// skill-writes behavior byte-for-byte.
/// </summary>
public sealed class BootstrapPrinciplesTransferTests
{
    private const string ComposedContent =
        "# Coding Principles\nAUTHORED-GOLD core + delta\n## Project Specifics (ratified additions)\n";

    private const string PrinciplesPath = ".agentsmith/contexts/server/coding-principles.md";

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
    public async Task Bootstrap_DoesNotGeneratePrinciplesFromArchaeology_TransfersAndRatifies()
    {
        var captured = new CapturedPrompt();
        var sandbox = new StubSandbox();
        var handler = NewHandler(captured, PrinciplesTransferStubs.Composing(ComposedContent));
        // Existing context.yaml makes the round's context.yaml verification pass,
        // isolating the assertion on the principles flow.
        var pipeline = NewPipeline(sandbox);

        var result = await handler.ExecuteAsync(
            new BootstrapRoundContext(BootstrapSkill.Name, "monorepo", new AgentConfig(), pipeline,
                ContextName: "server", Workdir: "server"),
            CancellationToken.None);

        // The framework wrote the AUTHORED composition — not the LLM.
        var written = sandbox.RanSteps.Single(
            s => s.Kind == StepKind.WriteFile && s.Path == PrinciplesPath);
        written.Content.Should().Be(ComposedContent);
        // The skill is asked for facts (context.yaml) + ratification, never
        // for a write_file of the principles.
        captured.User.Should().NotContain($"`{PrinciplesPath}` — use the `write_file` tool");
        captured.User.Should().Contain("RATIFY");
        captured.User.Should().Contain("Project Specifics");
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().Contain("transferred from the authored core+delta");
    }

    [Fact]
    public async Task ReInit_TransferMode_NeverOverwritesRatifiedPrinciples()
    {
        var captured = new CapturedPrompt();
        var sandbox = new StubSandbox();
        var handler = NewHandler(
            captured, PrinciplesTransferStubs.Composing(ComposedContent),
            existingPrinciples: "RATIFIED-BY-OPERATOR rules");
        var pipeline = NewPipeline(sandbox);

        var result = await handler.ExecuteAsync(
            new BootstrapRoundContext(BootstrapSkill.Name, "monorepo", new AgentConfig(), pipeline,
                ContextName: "server", Workdir: "server"),
            CancellationToken.None);

        sandbox.RanSteps.Should().NotContain(
            s => s.Kind == StepKind.WriteFile && s.Path == PrinciplesPath,
            "ratified principles are never overwritten on re-init");
        captured.User.Should().Contain("preserved as ratified");
        // The existing principles are framework-owned now — not embedded for
        // the LLM to rewrite (the p0202d merge stays context.yaml-only).
        captured.User.Should().NotContain("RATIFIED-BY-OPERATOR");
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().Contain("preserved");
    }

    [Fact]
    public async Task LegacyCatalog_NoTemplates_SkillWritesPrinciplesAsBefore()
    {
        var captured = new CapturedPrompt();
        var sandbox = new StubSandbox();
        var handler = NewHandler(captured, PrinciplesTransferStubs.NoTemplates());
        var pipeline = NewPipeline(sandbox);

        var result = await handler.ExecuteAsync(
            new BootstrapRoundContext(BootstrapSkill.Name, "monorepo", new AgentConfig(), pipeline,
                ContextName: "server", Workdir: "server"),
            CancellationToken.None);

        // Pre-p0379 shape: the prompt still names coding-principles.md as the
        // skill's write_file target and the zero-write_file failure stays.
        captured.User.Should().Contain($"`{PrinciplesPath}` — use the `write_file` tool");
        sandbox.RanSteps.Should().NotContain(
            s => s.Kind == StepKind.WriteFile && s.Path == PrinciplesPath);
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("write_file");
    }

    [Fact]
    public async Task TransferWriteFailure_FailsTheRoundLoudly()
    {
        var captured = new CapturedPrompt();
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Step step, IProgress<StepEvent>? _, CancellationToken _) => new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 1,
                TimedOut: false, DurationSeconds: 0.01, ErrorMessage: "disk full", OutputContent: null));
        var handler = NewHandler(captured, PrinciplesTransferStubs.Composing(ComposedContent));
        var pipeline = NewPipeline(sandbox.Object);

        var result = await handler.ExecuteAsync(
            new BootstrapRoundContext(BootstrapSkill.Name, "monorepo", new AgentConfig(), pipeline,
                ContextName: "server", Workdir: "server"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("BootstrapPrinciplesTransfer").And.Contain("disk full");
        captured.User.Should().BeEmpty("a failed transfer must fail before the LLM call");
    }

    [Fact]
    public async Task Transfer_UsesDiscoveredComponentLanguage_ForDeltaSelection()
    {
        string? seenSlug = null;
        var templates = new Mock<IPrinciplesTemplateSource>();
        templates.Setup(t => t.Compose(It.IsAny<string>()))
            .Returns((string slug) =>
            {
                seenSlug = slug;
                return new ComposedPrinciples(ComposedContent, slug, DeltaApplied: true);
            });
        var transfer = new BootstrapPrinciplesTransfer(
            templates.Object, NullLogger<BootstrapPrinciplesTransfer>.Instance);
        var handler = NewHandler(new CapturedPrompt(), transfer);
        var pipeline = NewPipeline(new StubSandbox());
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents,
            new Dictionary<string, IReadOnlyList<DiscoveredComponent>>(StringComparer.Ordinal)
            {
                ["monorepo"] = [new DiscoveredComponent("server", "server", "rust", "server/Cargo.toml")],
            });

        await handler.ExecuteAsync(
            new BootstrapRoundContext(BootstrapSkill.Name, "monorepo", new AgentConfig(), pipeline,
                ContextName: "server", Workdir: "server"),
            CancellationToken.None);

        seenSlug.Should().Be("rust", "the per-component discovery language wins over the repo-level map");
    }

    private static BootstrapRoundHandler NewHandler(
        CapturedPrompt captured, BootstrapPrinciplesTransfer transfer,
        string? existingPrinciples = null) => new(
        new PromptCapturingFactory(new CapturingChatClient(captured)),
        new BootstrapToolHostFactory(
            Mock.Of<IDecisionLogger>(),
            new PathReadGuard(new NullGitIgnoreResolver()),
            new PathWriteGuard(new PathReadGuard(new NullGitIgnoreResolver())),
            Mock.Of<IContextYamlSerializer>(),
            ContextGates.Build()),
        BootstrapReaderStubs.ReaderFactoryReturning(
            contextYaml: "meta:\n  workdir: server\n", codingPrinciples: existingPrinciples),
        transfer,
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
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles, new[] { BootstrapSkill });
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("main"), "https://x/y.git"));
        return pipeline;
    }

    private static ProjectMap NewMap(string primaryLanguage = "csharp") =>
        new(PrimaryLanguage: primaryLanguage,
            Frameworks: [],
            Modules: [],
            TestProjects: [],
            EntryPoints: [],
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
        public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null, MasterLoopHooks? masterLoopHooks = null) => client;
        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 8192;
        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";
    }
}
