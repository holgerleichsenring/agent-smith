using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0202 + p0202a: InstallDependenciesHandler runs each context's
/// ci.install_command in its sandbox before the Test step. The command is the
/// operator-owned value read from context.yaml at discovery time
/// (RemoteContextDiscovery.InstallCommand) — available at the handler's early
/// pipeline slot, unlike the analyzer's ProjectMap. Empty command skips
/// cleanly; non-zero exit aggregates into a single failure naming the repos.
/// </summary>
public sealed class InstallDependenciesHandlerTests
{
    private readonly InstallDependenciesHandler _handler =
        new(NullLogger<InstallDependenciesHandler>.Instance);

    [Fact]
    public async Task InstallDependenciesHandler_EmptyInstallCommand_ReturnsOk_LogsSkip()
    {
        var captured = new List<Step>();
        var pipeline = BuildPipeline(new()
        {
            ["default"] = new Ctx(InstallCommand: null, Workdir: ".", Sandbox: BuildSandbox(captured, 0)),
        });

        var result = await _handler.ExecuteAsync(new InstallDependenciesContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("skipped all");
        captured.Should().BeEmpty("no install command means no step is run");
    }

    [Fact]
    public async Task InstallDependenciesHandler_PerContextCommandResolved_RunsInWorkdir()
    {
        var captured = new List<Step>();
        var pipeline = BuildPipeline(new()
        {
            ["default"] = new Ctx(InstallCommand: "npm ci", Workdir: "frontend", Sandbox: BuildSandbox(captured, 0)),
        });

        var result = await _handler.ExecuteAsync(new InstallDependenciesContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var step = captured.Should().ContainSingle().Subject;
        step.Command.Should().Be("npm");
        step.Args.Should().Equal("ci");
        step.WorkingDirectory.Should().Be("/work/frontend");
    }

    [Fact]
    public async Task InstallDependenciesHandler_NonZeroExit_FailsWithRepoName()
    {
        var captured = new List<Step>();
        var pipeline = BuildPipeline(new()
        {
            ["api"] = new Ctx(InstallCommand: "npm ci", Workdir: ".", Sandbox: BuildSandbox(captured, 1)),
        });

        var result = await _handler.ExecuteAsync(new InstallDependenciesContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("api");
        result.Message.Should().Contain("1/1");
    }

    [Fact]
    public async Task InstallDependenciesHandler_MultipleRepos_AggregatesPerRepoOutcomes()
    {
        var captured = new List<Step>();
        var pipeline = BuildPipeline(new()
        {
            ["ok"] = new Ctx("npm ci", ".", BuildSandbox(captured, 0)),
            ["bad"] = new Ctx("pip install -r requirements.txt", ".", BuildSandbox(captured, 2)),
            ["nodeps"] = new Ctx(null, ".", BuildSandbox(captured, 0)),
        });

        var result = await _handler.ExecuteAsync(new InstallDependenciesContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("bad");
        result.Message.Should().NotContain("nodeps", "the skipped no-command repo is not part of the ran count");
        result.Message.Should().Contain("1/2", "one of two repos with a command failed");
    }

    private sealed record Ctx(string? InstallCommand, string Workdir, ISandbox Sandbox);

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
                kv => new RemoteContextDiscovery(kv.Key, kv.Value.Workdir, "polyglot", kv.Value.InstallCommand),
                StringComparer.Ordinal));
        return pipeline;
    }
}
