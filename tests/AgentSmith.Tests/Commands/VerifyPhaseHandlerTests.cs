using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Commands;

// p0400: the build gate never guesses a filename. Declared context commands win;
// a .NET repo without them gets its entry point DISCOVERED from files that exist,
// and an ambiguous/absent entry point is a named resolution failure — not a
// compile result. A ships_code:false phase with no diff skips the gate entirely.
public sealed class VerifyPhaseHandlerTests
{
    private static VerifyPhaseHandler Handler() => new(
        new SandboxGitOperations(
            NullLogger<SandboxGitOperations>.Instance, new SandboxFileReaderFactory(), new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance)),
        new SandboxFileReaderFactory(),
        new SandboxTargets(),
        new VerifyCommandRunner(NullLogger<VerifyCommandRunner>.Instance),
        NullLogger<VerifyPhaseHandler>.Instance);

    private static ProjectMap Map(string language, CiConfig ci) => new(
        language, [], [], [], [], new Conventions(null, null, null), ci);

    private static (VerifyPhaseContext Context, ScriptedSandbox Sandbox) Setup(
        ProjectMap? map, PhaseDraft? draft = null, string workdir = ".")
    {
        var pipeline = new PipelineContext();
        var sandbox = new ScriptedSandbox();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["server"] = sandbox });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>
            {
                ["server"] = new("default", workdir, "csharp"),
            });
        if (draft is not null) pipeline.Set(ContextKeys.PhaseSpec, draft);
        var maps = map is null
            ? new Dictionary<string, ProjectMap>()
            : new Dictionary<string, ProjectMap> { ["server"] = map };
        return (new VerifyPhaseContext(maps, pipeline), sandbox);
    }

    [Fact]
    public async Task VerifyPhase_ShipsCodeFalse_NoDiff_SkipsBuild()
    {
        var draft = new PhaseDraft("p9901", "inventory", "yaml", []) { ShipsCode = false };
        var (context, sandbox) = Setup(
            Map("csharp", new CiConfig(true, "dotnet build", "dotnet test", null)), draft);

        var result = await Handler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("ships_code");
        sandbox.RanSteps.Should().NotContain(s => s.Command == "dotnet",
            "a knowledge phase that changed nothing has nothing the build gate could measure");
    }

    [Fact]
    public async Task VerifyPhase_ShipsCodeTrue_DeclaredCommands_StillRun()
    {
        var (context, sandbox) = Setup(
            Map("csharp", new CiConfig(true, "dotnet build", null, null)));

        var result = await Handler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sandbox.RanSteps.Should().Contain(s => s.Command == "dotnet");
    }

    // p0400a: declared ci commands are authored against the repo root (run b9b0:
    // executing them at the context workdir turned a green baseline into MSB1009).
    [Fact]
    public async Task VerifyPhase_DeclaredCommand_RunsAtRepoRoot()
    {
        var (context, sandbox) = Setup(
            Map("csharp", new CiConfig(true, "dotnet build Sample.sln", null, null)),
            workdir: "Sample.Api");

        await Handler().ExecuteAsync(context, CancellationToken.None);

        var build = sandbox.RanSteps.Single(s => s.Command == "dotnet");
        build.WorkingDirectory.Should().Be(Repository.SandboxWorkPath,
            "the analyzer authored the command against the repo root, where the master proved it green");
    }

    [Fact]
    public async Task VerifyPhase_DiscoveredEntryPoint_RunsAtContextWorkdir()
    {
        var (context, sandbox) = Setup(
            Map("csharp", new CiConfig(false, null, null, null)), workdir: "Sample.Api");
        sandbox.ListFilesJson = """["Sample.sln"]""";

        await Handler().ExecuteAsync(context, CancellationToken.None);

        var build = sandbox.RanSteps.Single(s => s.Command == "dotnet");
        build.WorkingDirectory.Should().Be($"{Repository.SandboxWorkPath}/Sample.Api",
            "a discovered entry point's path is relative to where it was found");
    }

    [Fact]
    public async Task VerifyCommandResolution_NoContextCommand_DiscoversSingleSln()
    {
        var (context, sandbox) = Setup(Map("csharp", new CiConfig(false, null, null, null)));
        sandbox.ListFilesJson = """["Sample.sln", "src/Sample.Api.csproj"]""";

        var result = await Handler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var build = sandbox.RanSteps.Single(s => s.Command == "dotnet");
        build.Args.Should().ContainInOrder("build", "Sample.sln");
    }

    [Fact]
    public async Task VerifyCommandResolution_NoSlnFound_NamedResolutionFinding_NotBuildFailure()
    {
        var (context, sandbox) = Setup(Map("csharp", new CiConfig(false, null, null, null)));
        sandbox.ListFilesJson = """["README.md", "docs/notes.md"]""";

        var result = await Handler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("resolution failure").And.Contain("searched");
        result.Message.Should().NotContain("exited", "no command ran, so there is no compile result");
        sandbox.RanSteps.Should().NotContain(s => s.Command == "dotnet",
            "a filename is never invented when the entry point cannot be resolved");
    }

    [Fact]
    public async Task VerifyCommandResolution_NonDotnetRepoWithoutCommands_SkippedNotFailed()
    {
        // p0393 rationale preserved: docs/infra repos declaring no commands are
        // skipped — discovery only applies where the map says .NET.
        var (context, sandbox) = Setup(Map("generic", new CiConfig(false, null, null, null)));

        var result = await Handler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sandbox.RanSteps.Should().NotContain(s => s.Command == "dotnet");
    }

    private sealed class ScriptedSandbox : ISandbox
    {
        public string JobId => "verify-test";
        public List<Step> RanSteps { get; } = new();
        public string GitStatusOutput { get; set; } = string.Empty;
        public string ListFilesJson { get; set; } = "[]";

        public Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            RanSteps.Add(step);
            var output = step.Kind switch
            {
                StepKind.ListFiles => ListFilesJson,
                StepKind.Run when step.Command == "git"
                    && step.Args!.Contains("status") => GitStatusOutput,
                _ => string.Empty,
            };
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0,
                TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: output));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
