using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Infrastructure.Services.Activation;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-23-4711: a re-init derives a new set of components and writes it, and the context
/// the previous derivation produced used to stay standing beside them — rounds are fanned out
/// per DERIVED component, so a declared context the derivation dropped was never visited.
/// After the rounds, what the tree carries and the derivation did not produce is MOVED aside,
/// never deleted, and named in the run's record.
/// </summary>
public sealed class BootstrapContextRetirementTests
{
    private const string Repo = "primary";
    private const string ContextsRoot = "/work/.agentsmith/contexts";
    private const string RetiredRoot = "/work/.agentsmith/contexts-retired";
    private const string Ratified = "# Principles\nthe operator's ratified rules\n";

    [Fact]
    public async Task Bootstrap_AContextTheDerivationDropped_IsMovedAside()
    {
        var tree = TreeWith("api", "legacy");

        var result = await RetireAsync(tree, derived: "api");

        tree.Files.Keys.Should().NotContain(k => k.StartsWith($"{ContextsRoot}/legacy/", StringComparison.Ordinal),
            "the name the derivation dropped no longer stands beside the ones that replaced it");
        tree.Files[$"{RetiredRoot}/legacy/principles.md"].Should().Be(Ratified,
            "a retirement is a MOVE — the content is in the pull request, recoverable, not deleted");
        tree.Files.Should().ContainKey($"{RetiredRoot}/legacy/context.yaml");
        MoveScripts(tree).Should().ContainSingle()
            .Which.Should().Contain("git mv").And.Contain("|| mv",
                "the move reads as a rename in the pull request, and an untracked directory "
                + "still moves rather than failing the step");
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().Contain("legacy");
    }

    [Fact]
    public async Task Bootstrap_AContextTheDerivationKept_IsUntouched()
    {
        var tree = TreeWith("api", "legacy");

        await RetireAsync(tree, derived: "api");

        tree.Files.Should().ContainKey($"{ContextsRoot}/api/principles.md",
            "a context this derivation produced stays where the gate probes for it");
        tree.Files.Should().ContainKey($"{ContextsRoot}/api/context.yaml");
        tree.Files[$"{ContextsRoot}/api/principles.md"].Should().Be(Ratified);
        tree.Files.Keys.Should().NotContain(k => k.StartsWith($"{RetiredRoot}/api/", StringComparison.Ordinal),
            "a context this derivation produced is not touched by the pass that retires the rest");
        MoveScripts(tree).Should().NotContain(s => s.Contains("/api", StringComparison.Ordinal));
    }

    /// <summary>
    /// The retirement is the LAST command the fan-out emits, and it is not a finalizer — so a
    /// round that fails stops the pipeline before it, and the tree is left exactly as it was.
    /// </summary>
    [Fact]
    public async Task Bootstrap_ARoundThatFailed_RetiresNothing()
    {
        var pipeline = DispatchPipeline();
        var dispatched = await Dispatch().ExecuteAsync(
            new BootstrapDispatchContext(pipeline), CancellationToken.None);
        dispatched.InsertNext!.Last().Name.Should().Be(CommandNames.BootstrapRetire,
            "the retirement follows every round the fan-out emits");

        var executed = new List<string>();
        var harness = new PipelineExecutorTestBuilder(sandboxCoordinator: NoSandbox());
        harness.FactoryMock
            .Setup(f => f.Create(It.IsAny<PipelineCommand>(), It.IsAny<ResolvedProject>(), It.IsAny<PipelineContext>()))
            .Returns((PipelineCommand c, ResolvedProject _, PipelineContext _) => new NamedContext(c.Name));
        harness.ExecutorMock
            .Setup(e => e.ExecuteAsync(It.IsAny<ICommandContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ICommandContext c, CancellationToken _) =>
            {
                var name = ((NamedContext)c).Command;
                executed.Add(name);
                if (name == CommandNames.BootstrapDispatch) return dispatched;
                return name == CommandNames.BootstrapRound
                    ? CommandResult.Fail("BootstrapRound: the producer wrote nothing.")
                    : CommandResult.Ok("done");
            });

        var run = await harness.Sut.ExecuteAsync(
            new[] { CommandNames.BootstrapDispatch, CommandNames.WriteRunResult },
            new ResolvedProject(), pipeline, CancellationToken.None);

        run.IsSuccess.Should().BeFalse();
        executed.Should().Contain(CommandNames.BootstrapRound);
        executed.Should().Contain(CommandNames.WriteRunResult,
            "the record is written for a failed run — the finalizer tail is what still runs");
        executed.Should().NotContain(CommandNames.BootstrapRetire,
            "a half-written derivation retires nothing: the retirement runs after the rounds "
            + "and is not a finalizer, so a failed round leaves every context where it was");
    }

    [Fact]
    public async Task Bootstrap_ARetiredContext_IsNamedInTheResult()
    {
        var tree = TreeWith("api", "legacy");
        var pipeline = RetirePipeline(tree, "api");
        await Handler().ExecuteAsync(new BootstrapRetireContext(pipeline), CancellationToken.None);

        var retired = pipeline.Get<IReadOnlyDictionary<string, IReadOnlyList<string>>>(
            ContextKeys.RetiredContexts);
        var rendered = RunResultFormatter.FormatInitResult(
            runId: "2026-09-23T10-00-00-4711", durationSeconds: 1, costSummary: null, trail: null,
            repoName: Repo, retiredContexts: retired[Repo]);

        rendered.Should().Contain("Retired contexts");
        rendered.Should().Contain("legacy",
            "a person reading the pull request is TOLD a context was set aside, rather than "
            + "having to notice it by its absence");
        rendered.Should().Contain(".agentsmith/contexts-retired/legacy");
        rendered.Should().NotContain("| api |", "the kept context is not reported as retired");
    }

    private static IReadOnlyList<string> MoveScripts(MetaTree tree) =>
        [.. tree.RanSteps
            .Where(s => s.Kind == StepKind.Run && s.Args is { Count: 2 })
            .Select(s => s.Args![1])
            .Where(script => script.Contains("mv ", StringComparison.Ordinal))];

    private static async Task<CommandResult> RetireAsync(MetaTree tree, string derived) =>
        await Handler().ExecuteAsync(
            new BootstrapRetireContext(RetirePipeline(tree, derived)), CancellationToken.None);

    private static BootstrapRetireHandler Handler() => new(
        new BootstrapContextRetirement(
            new ProjectMetaResolver(
                new ContextYamlParser(new ContextYamlSerializer(new ContextYamlBuilders()))),
            new MetaTreeReaderFactory(),
            NullLogger<BootstrapContextRetirement>.Instance),
        new SandboxTargets(),
        NullLogger<BootstrapRetireHandler>.Instance);

    private static MetaTree TreeWith(params string[] contextNames)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in contextNames)
        {
            files[$"{ContextsRoot}/{name}/context.yaml"] = $"meta:\n  workdir: {name}\n";
            files[$"{ContextsRoot}/{name}/principles.md"] = Ratified;
        }
        return new MetaTree(files);
    }

    private static PipelineContext RetirePipeline(MetaTree tree, params string[] derived)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents,
            new Dictionary<string, IReadOnlyList<DiscoveredComponent>>(StringComparer.Ordinal)
            {
                [Repo] = [.. derived.Select(d => new DiscoveredComponent(d, d, "csharp", $"{d}/x.csproj"))],
            });
        pipeline.Set<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos, [new RepoConnection { Name = Repo, Url = "https://x/y.git", Auth = "test" }]);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [Repo] = tree });
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxRepos,
            new Dictionary<string, string>(StringComparer.Ordinal) { [Repo] = Repo });
        return pipeline;
    }

    private static readonly ConceptVocabulary Vocab = new(new Dictionary<string, ProjectConcept>
    {
        ["pipeline_name"] = new(
            "pipeline_name", "test", ConceptType.Enum, new[] { "init-project" }, null, []),
        ["project_language"] = new("project_language", "test", ConceptType.String, null, null, []),
    });

    private static BootstrapDispatchHandler Dispatch() => new(
        new BootstrapRoundMatch(
            new ActivationSkillFilter(
                new ActivationExpressionParser(new ActivationExpressionTokenizer()),
                new ActivationEvaluator(),
                NullLogger<ActivationSkillFilter>.Instance),
            NullLogger<BootstrapRoundMatch>.Instance),
        context => new PipelineContextRunStateConcepts(context, Vocab));

    private static PipelineContext DispatchPipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "2026-09-23T10-00-00-4711");
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            "init-project", new AgentConfig(), "skills", null));
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles, new[]
        {
            new RoleSkillDefinition
            {
                Name = "csharp-bootstrap",
                ActivatesWhen = "pipeline_name = \"init-project\" AND project_language = \"csharp\"",
                OutputSchema = "bootstrap",
            },
        });
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents,
            new Dictionary<string, IReadOnlyList<DiscoveredComponent>>(StringComparer.Ordinal)
            {
                [Repo] = [new DiscoveredComponent("api", ".", "csharp", "api/Api.csproj")],
            });
        new PipelineContextRunStateConcepts(pipeline, Vocab).SetEnum("pipeline_name", "init-project");
        return pipeline;
    }

    // Provisioning is not under test: the executor would otherwise want a staged repo
    // inventory before the first sandbox-requiring command.
    private static IPipelineSandboxCoordinator NoSandbox()
    {
        var mock = new Mock<IPipelineSandboxCoordinator>();
        mock.Setup(c => c.IsSandboxRequiring(It.IsAny<string>())).Returns(false);
        mock.Setup(c => c.RequiresSandbox(It.IsAny<IEnumerable<PipelineCommand>>())).Returns(false);
        return mock.Object;
    }

    private sealed record NamedContext(string Command) : ICommandContext;

    private sealed class MetaTreeReaderFactory : ISandboxFileReaderFactory
    {
        public ISandboxFileReader Create(ISandbox sandbox) => (MetaTree)sandbox;
    }

    /// <summary>
    /// One repository's meta tree, listed and moved through the same file map — so a move
    /// performed by a step is visible to the read that follows it, as it is in /work.
    /// </summary>
    private sealed class MetaTree(Dictionary<string, string> files) : ISandbox, ISandboxFileReader
    {
        public Dictionary<string, string> Files { get; } = files;

        public List<Step> RanSteps { get; } = [];

        public string JobId => "meta-tree";

        public Task<bool> ExistsAsync(string path, CancellationToken ct) =>
            Task.FromResult(Files.ContainsKey(path));

        public Task<string?> TryReadAsync(string path, CancellationToken ct) =>
            Task.FromResult(Files.GetValueOrDefault(path));

        public Task<string> ReadRequiredAsync(string path, CancellationToken ct) =>
            Task.FromResult(Files[path]);

        public Task WriteAsync(string path, string content, CancellationToken ct)
        {
            Files[path] = content;
            return Task.CompletedTask;
        }

        /// <summary>Every file under the path, plus the directories on the way to it.</summary>
        public Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken ct)
        {
            var entries = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var file in Files.Keys.Where(f => f.StartsWith(path + "/", StringComparison.Ordinal)))
                for (var i = file.IndexOf('/', path.Length + 1); i > 0; i = file.IndexOf('/', i + 1))
                    entries.Add(file[..i]);
            return Task.FromResult<IReadOnlyList<string>>([.. entries]);
        }

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            RanSteps.Add(step);
            if (step.Kind == StepKind.Run) ApplyMove(step.Args is { Count: 2 } args ? args[1] : string.Empty);
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0,
                TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: null));
        }

        // `mv <dir> <dir>` semantics for the one script the retirement issues: every file
        // under the source directory ends up under the target, and none is left behind.
        private void ApplyMove(string script)
        {
            var source = Files.Keys
                .Select(k => k[..k.LastIndexOf('/')])
                .FirstOrDefault(dir => script.Contains($"'{dir}'", StringComparison.Ordinal));
            if (source is null) return;
            var target = RetiredRoot + source[ContextsRoot.Length..];
            foreach (var file in Files.Keys.Where(f => f.StartsWith(source + "/", StringComparison.Ordinal)).ToList())
            {
                Files[target + file[source.Length..]] = Files[file];
                Files.Remove(file);
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
