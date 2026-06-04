using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0202 + p0202a: EnsurePrerequisitesHandler runs each context's
/// prerequisites in its sandbox before the Test step. The command is the
/// operator-owned value read from context.yaml at discovery time
/// (RemoteContextDiscovery.Prerequisites) — available at the handler's early
/// pipeline slot, unlike the analyzer's ProjectMap. Empty command skips
/// cleanly; non-zero exit aggregates into a single failure naming the repos.
/// </summary>
public sealed class EnsurePrerequisitesHandlerTests
{
    private readonly EnsurePrerequisitesHandler _handler =
        new(NullLogger<EnsurePrerequisitesHandler>.Instance);

    [Fact]
    public async Task EnsurePrerequisitesHandler_EmptyPrerequisites_ReturnsOk_LogsSkip()
    {
        var captured = new List<Step>();
        var pipeline = BuildPipeline(new()
        {
            ["default"] = new Ctx(Prerequisites: null, Workdir: ".", Sandbox: BuildSandbox(captured, 0)),
        });

        var result = await _handler.ExecuteAsync(new EnsurePrerequisitesContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("skipped all");
        captured.Should().BeEmpty("no install command means no step is run");
    }

    [Fact]
    public async Task EnsurePrerequisitesHandler_PerContextCommandResolved_RunsInWorkdir()
    {
        var captured = new List<Step>();
        var pipeline = BuildPipeline(new()
        {
            ["default"] = new Ctx(Prerequisites: "npm ci", Workdir: "frontend", Sandbox: BuildSandbox(captured, 0)),
        });

        var result = await _handler.ExecuteAsync(new EnsurePrerequisitesContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var step = captured.Should().ContainSingle().Subject;
        step.Command.Should().Be("npm");
        step.Args.Should().Equal("ci");
        step.WorkingDirectory.Should().Be("/work/frontend");
    }

    [Fact]
    public async Task EnsurePrerequisitesHandler_NonZeroExit_FailsWithRepoName()
    {
        var captured = new List<Step>();
        var pipeline = BuildPipeline(new()
        {
            ["api"] = new Ctx(Prerequisites: "npm ci", Workdir: ".", Sandbox: BuildSandbox(captured, 1)),
        });

        var result = await _handler.ExecuteAsync(new EnsurePrerequisitesContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("api");
        result.Message.Should().Contain("1/1");
    }

    [Fact]
    public async Task EnsurePrerequisitesHandler_MultipleRepos_AggregatesPerRepoOutcomes()
    {
        var captured = new List<Step>();
        var pipeline = BuildPipeline(new()
        {
            ["ok"] = new Ctx("npm ci", ".", BuildSandbox(captured, 0)),
            ["bad"] = new Ctx("pip install -r requirements.txt", ".", BuildSandbox(captured, 2)),
            ["nodeps"] = new Ctx(null, ".", BuildSandbox(captured, 0)),
        });

        var result = await _handler.ExecuteAsync(new EnsurePrerequisitesContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("bad");
        result.Message.Should().NotContain("nodeps", "the skipped no-command repo is not part of the ran count");
        result.Message.Should().Contain("1/2", "one of two repos with a command failed");
    }

    [Fact]
    public async Task EnsurePrerequisitesHandler_NoOperatorPrerequisite_SkipsEvenWithAnalyzerMap()
    {
        // p0224: the analyzer-derived auto-install is gone — only an explicit
        // context.yaml prerequisite runs. With no operator command the step
        // skips (the coding-agent-master installs deps itself), even when an
        // analyzer ProjectMap with a prerequisite is present.
        var captured = new List<Step>();
        var pipeline = BuildPipeline(new()
        {
            ["default"] = new Ctx(Prerequisites: null, Workdir: ".", Sandbox: BuildSandbox(captured, 0)),
        });
        SeedProjectMap(pipeline, "default", prerequisites: "npm install",
            modulePaths: ["Sample.Client", "Sample.Client/src"]);

        var result = await _handler.ExecuteAsync(new EnsurePrerequisitesContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured.Should().BeEmpty("analyzer-derived prerequisites no longer run; the agent installs deps");
    }

    [Fact]
    public async Task EnsurePrerequisitesHandler_OperatorWorkdir_RunsInThatSubtree()
    {
        // p0224: the only location source is the operator's meta.workdir.
        var captured = new List<Step>();
        var pipeline = BuildPipeline(new()
        {
            ["default"] = new Ctx(Prerequisites: "npm ci", Workdir: "override-dir", Sandbox: BuildSandbox(captured, 0)),
        });

        await _handler.ExecuteAsync(new EnsurePrerequisitesContext(pipeline), CancellationToken.None);

        captured.Should().ContainSingle().Which.WorkingDirectory.Should().Be("/work/override-dir");
    }

    [Fact]
    public async Task EnsurePrerequisitesHandler_OverrideWinsOverAnalyzerDerived()
    {
        var captured = new List<Step>();
        var pipeline = BuildPipeline(new()
        {
            ["default"] = new Ctx(Prerequisites: "yarn install", Workdir: ".", Sandbox: BuildSandbox(captured, 0)),
        });
        SeedProjectMap(pipeline, "default", prerequisites: "npm install");

        await _handler.ExecuteAsync(new EnsurePrerequisitesContext(pipeline), CancellationToken.None);

        captured.Should().ContainSingle().Which.Command.Should().Be(
            "yarn", "the context.yaml override wins over the analyzer-derived command");
    }

    private static void SeedProjectMap(
        PipelineContext pipeline, string key, string? prerequisites, IReadOnlyList<string>? modulePaths = null)
    {
        var modules = (modulePaths ?? Array.Empty<string>())
            .Select(p => new Module(p, ModuleRole.Production, [])).ToArray();
        var map = new ProjectMap("polyglot", [], modules, [], [], new Conventions(null, null, null),
            new CiConfig(HasCi: true, BuildCommand: null, TestCommand: null, CiSystem: null), Prerequisites: prerequisites);
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal) { [key] = map });
    }

    private sealed record Ctx(string? Prerequisites, string Workdir, ISandbox Sandbox);

    private static ISandbox BuildSandbox(List<Step> captured, int exitCode)
    {
        var mock = new Mock<ISandbox>();
        mock.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                captured.Add(step);
                return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, exitCode, false, 0.1, null));
            });
        return mock.Object;
    }

    private static PipelineContext BuildPipeline(Dictionary<string, Ctx> contexts)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            contexts.ToDictionary(kv => kv.Key, kv => kv.Value.Sandbox, StringComparer.Ordinal));
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            contexts.ToDictionary(
                kv => kv.Key,
                kv => new RemoteContextDiscovery(kv.Key, kv.Value.Workdir, "polyglot", kv.Value.Prerequisites),
                StringComparer.Ordinal));
        return pipeline;
    }
}
