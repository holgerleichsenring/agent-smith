using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
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
// compile result. p0430: a branch carrying no source change skips the gate entirely —
// read from the branch, never from a declaration.
public sealed class VerifyPhaseHandlerTests
{
    private static VerifyPhaseHandler Handler() => new(
        new VerifyStageResolver(
            new DotnetEntryPointDiscovery(
                new SandboxFileReaderFactory(), NullLogger<DotnetEntryPointDiscovery>.Instance),
            NullLogger<VerifyStageResolver>.Instance),
        new DomainProfileStagesResolver(AgentSmith.Tests.TestHelpers.TestDomainProfiles.Empty),
        new SandboxTargets(),
        new VerifyCommandRunner(NullLogger<VerifyCommandRunner>.Instance),
        new DeliveryDiff(NullLogger<DeliveryDiff>.Instance),
        new PhaseAccounting(
            new DeliveryDiff(NullLogger<DeliveryDiff>.Instance),
            new SpecAccountant(
                new AgentSmith.Tests.TestHelpers.ScriptedChatClientFactory(),
                new SpecAccountCall(new AgentSmith.Tests.TestHelpers.ScriptedChatClientFactory(), new AgentSmith.Application.Services.Events.AsyncLocalRunContextAccessor(), NullLogger<SpecAccountCall>.Instance),
                NullLogger<SpecAccountant>.Instance),
            new SandboxTargets(),
            NullLogger<PhaseAccounting>.Instance),
        new PhaseProgressRecorder(new NoOpEventPublisher()),
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

    /// <summary>
    /// p0421: a phase that touched nothing skips the mechanical gates — read from the
    /// TREE, not from a ships_code declaration. The declaration existed only to except
    /// the old gate from its own question; the tree answers it directly, and a phase
    /// that committed as it went is not "untouched" (its checkpoint says otherwise).
    /// </summary>
    [Fact]
    public async Task VerifyPhase_UntouchedTree_SkipsBuild()
    {
        var (context, sandbox) = Setup(
            Map("csharp", new CiConfig(true, "dotnet build Sample.sln", null, null)));
        sandbox.GitStatusOutput = string.Empty;

        var result = await Handler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sandbox.RanSteps.Should().NotContain(s => IsDotnet(s),
            "there is nothing for a build to be green about when nothing changed");
    }

    [Fact]
    public async Task VerifyPhase_ADeliveringBranch_RunsTheDeclaredCommands()
    {
        var (context, sandbox) = Setup(
            Map("csharp", new CiConfig(true, "dotnet build", null, null)));

        var result = await Handler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sandbox.RanSteps.Should().Contain(s => IsDotnet(s));
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

        var build = sandbox.RanSteps.Single(IsDotnet);
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

        var build = sandbox.RanSteps.Single(IsDotnet);
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
        var build = sandbox.RanSteps.Single(IsDotnet);
        CommandLineOf(build).Should().Contain("build").And.Contain("Sample.sln");
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
        sandbox.RanSteps.Should().NotContain(s => IsDotnet(s),
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
        sandbox.RanSteps.Should().NotContain(s => IsDotnet(s));
    }

    /// <summary>
    /// p0425: a declared verification command runs as a command LINE through /bin/sh -c,
    /// the same way the agent's own run_command tool does. Tokenising it into argv handed
    /// `&amp;&amp;` to MSBuild and failed ticket 19192 on its own separator.
    /// </summary>
    private static bool IsDotnet(Step step) => CommandLineOf(step).StartsWith("dotnet");

    private static string CommandLineOf(Step step) =>
        step.Command == "/bin/sh" && step.Args is { Count: 2 } args ? args[1] : step.Command ?? "";

    private sealed class ScriptedSandbox : ISandbox
    {
        public string JobId => "verify-test";
        public List<Step> RanSteps { get; } = new();
        // p0422: a verify runs after a phase that changed something, and the gate reads
        // the BRANCH — so the default fixture is a branch carrying a source change. A
        // test about the untouched case says so explicitly.
        public string GitStatusOutput { get; set; } =
            "diff --git a/src/Api/Program.cs b/src/Api/Program.cs\n"
            + "--- a/src/Api/Program.cs\n+++ b/src/Api/Program.cs\n@@ -1 +1 @@\n+changed\n";
        public string ListFilesJson { get; set; } = "[]";

        public Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            RanSteps.Add(step);
            var output = step.Kind switch
            {
                StepKind.ListFiles => ListFilesJson,
                StepKind.Run when step.Command == "git" && step.Args!.Contains("diff")
                    => GitStatusOutput,
                StepKind.Run when step.Command == "git" && step.Args!.Contains("status")
                    => GitStatusOutput,
                _ => string.Empty,
            };
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0,
                TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: output));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
