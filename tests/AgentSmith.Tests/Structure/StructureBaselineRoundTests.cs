using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
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
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Structure;

/// <summary>
/// 2026-10-06-8565c: a whole bootstrap round over an in-memory repository — the transfer writes
/// principles.md, the scripted skill rewrites it, and the baseline measures what the init commits.
/// </summary>
public sealed class StructureBaselineRoundTests
{
    private const string Principles = ".agentsmith/contexts/server/principles.md";
    private const string Lenient = "## Limits\n\n```yaml\nfunction_lines: 100\n```\n";
    private const string Strict = "## Limits\n\n```yaml\nfunction_lines: 4\n```\n";
    private const string Oversized = "class A\n{\n    void Long()\n    {\n        a();\n        b();\n        c();\n    }\n}\n";

    [Fact]
    public async Task BootstrapRound_SkillRewritesPrinciples_BaselineMeasuresTheFinalText()
    {
        var repo = new RepoSandbox();
        repo.Files["src/A.cs"] = Oversized;
        var pipeline = Pipeline(repo);

        var result = await Handler(repo).ExecuteAsync(
            new BootstrapRoundContext("project-bootstrap", "monorepo", new AgentConfig(), pipeline, "server", "."),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        repo.Files[".agentsmith/contexts/server/structure-baseline.tsv"].Should().Contain("A.Long()");
        InitPullRequestBody.Compose(pipeline, "monorepo", "<!-- siblings -->")
            .Should().Contain("1 unit(s) already exceed the Limits");
    }

    [Fact]
    public async Task StructureBaseline_WriteFails_RoundGoesOnAndSaysSo()
    {
        var repo = new RepoSandbox();
        repo.Files["src/A.cs"] = Oversized;
        repo.FailingWrites.Add(".agentsmith/contexts/server/structure-baseline.tsv");
        var pipeline = Pipeline(repo);

        var result = await Handler(repo).ExecuteAsync(
            new BootstrapRoundContext("project-bootstrap", "monorepo", new AgentConfig(), pipeline, "server", "."),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        InitPullRequestBody.Compose(pipeline, "monorepo", "<!-- siblings -->")
            .Should().Contain("no structure baseline — measuring or writing it failed");
    }

    private static BootstrapRoundHandler Handler(RepoSandbox repo) => new(
        new SkillFactory(repo),
        new BootstrapToolHostFactory(
            Mock.Of<IDecisionLogger>(), new SandboxFileReaderFactory(),
            new PathReadGuard(new NullGitIgnoreResolver()),
            new PathWriteGuard(new PathReadGuard(new NullGitIgnoreResolver())),
            ContextGates.Serializer(), ContextGates.Build(), ContextGates.Writer(), ContextGates.DerivationStamp(),
            ContextGates.ImageReview()),
        BootstrapReaderStubs.MetaFilesOver(new SandboxFileReaderFactory()),
        PrinciplesTransferStubs.Composing(Lenient),
        new BootstrapContextWriteVerdict(),
        new BootstrapOutputRecorder(),
        StructureBaselineStubs.Real(),
        StructureBaselineStubs.Scopes(),
        new SandboxTargets(),
        EventTestStubs.RunContext,
        NullLogger<BootstrapRoundHandler>.Instance);

    private static PipelineContext Pipeline(ISandbox sandbox)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig("init-project", new AgentConfig(), "skills", null));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["monorepo"] = sandbox });
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal) { ["monorepo"] = Map() });
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles, [new RoleSkillDefinition
        {
            Name = "project-bootstrap", DisplayName = "Project Bootstrap", Description = "test", Emoji = "🔧",
            Rules = "test", Role = "producer", OutputSchema = "bootstrap",
        }]);
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("main"), "https://x/y.git"));
        return pipeline;
    }

    private static ProjectMap Map() => new(
        PrimaryLanguage: "csharp", Frameworks: [], Modules: [], TestProjects: [], EntryPoints: [],
        Conventions: new Conventions(NamingPattern: null, TestLayout: null, ErrorHandling: null),
        Ci: new CiConfig(HasCi: false, BuildCommand: null, TestCommand: null, CiSystem: null));

    // The skill writes context.yaml through its tool and, as a real one may, rewrites principles.md.
    private sealed class SkillFactory(RepoSandbox repo) : IChatClientFactory, IChatClient
    {
        public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null,
            MasterLoopHooks? masterLoopHooks = null) => this;
        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 8192;
        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            await BootstrapToolCall.WriteContextYamlAsync(options, BootstrapToolCall.ValidDocument);
            repo.Files[Principles] = Strict;
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
