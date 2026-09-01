using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
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
    private static VerifyPhaseHandler Handler(ISpecAccountant? accountant = null) => new(
        new VerifyStageResolver(
            new DotnetEntryPointDiscovery(
                new SandboxFileReaderFactory(), NullLogger<DotnetEntryPointDiscovery>.Instance),
            new DeclaredStagePresence(
                new SandboxFileReaderFactory(), NullLogger<DeclaredStagePresence>.Instance),
            NullLogger<VerifyStageResolver>.Instance),
        new ContextVerifyStagesResolver(),
        new SandboxTargets(),
        new VerifyCommandRunner(NullLogger<VerifyCommandRunner>.Instance),
        AgentSmith.Tests.TestHelpers.TestGit.Delivery,
        new PhaseAccounting(
            AgentSmith.Tests.TestHelpers.TestGit.Delivery,
            accountant ?? new SpecAccountant(
                new AgentSmith.Tests.TestHelpers.ScriptedChatClientFactory(),
                new AccountCalls(new SpecAccountCall(new AgentSmith.Tests.TestHelpers.ScriptedChatClientFactory(), new AgentSmith.Application.Services.Events.AsyncLocalRunContextAccessor(), NullLogger<SpecAccountCall>.Instance)),
                NullLogger<SpecAccountant>.Instance),
            new SandboxTargets(),
            NullLogger<PhaseAccounting>.Instance),
        new PhaseProgressRecorder(new NoOpEventPublisher()),
        NullLogger<VerifyPhaseHandler>.Instance);

    private static ProjectMap Map(string language, CiConfig ci) => new(
        language, [], [], [], [], new Conventions(null, null, null), ci);

    private static (VerifyPhaseContext Context, ScriptedSandbox Sandbox) Setup(
        ProjectMap? map, PhaseDraft? draft = null, string workdir = ".",
        params RemoteContextDiscovery[] contexts)
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
        // 2026-08-31-26d4: the per-sandbox CONTEXT LIST is what the declaration is read
        // from — the representative above is deliberately not it.
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>
            {
                ["server"] = contexts.Length > 0
                    ? contexts
                    : [new RemoteContextDiscovery("default", workdir, "csharp")],
            });
        if (draft is not null) pipeline.Set(ContextKeys.PhaseSpec, draft);
        var maps = map is null
            ? new Dictionary<string, ProjectMap>()
            : new Dictionary<string, ProjectMap> { ["server"] = map };
        return (new VerifyPhaseContext(maps, pipeline), sandbox);
    }

    /// <summary>
    /// A two-repository run: the shape the narrowed rule is about, where one repository
    /// being unverifiable is not the same as the run being unverified.
    /// </summary>
    private static (VerifyPhaseContext Context, IReadOnlyDictionary<string, ScriptedSandbox> Sandboxes)
        SetupTwo(params (string Key, ProjectMap Map)[] repos)
    {
        var pipeline = new PipelineContext();
        var sandboxes = repos.ToDictionary(r => r.Key, _ => new ScriptedSandbox());
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, sandboxes.ToDictionary(e => e.Key, e => (ISandbox)e.Value));
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            repos.ToDictionary(r => r.Key, r => Discovery(r.Map)));
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts,
            repos.ToDictionary(
                r => r.Key, r => (IReadOnlyList<RemoteContextDiscovery>)[Discovery(r.Map)]));
        return (
            new VerifyPhaseContext(repos.ToDictionary(r => r.Key, r => r.Map), pipeline),
            sandboxes);
    }

    private static RemoteContextDiscovery Discovery(ProjectMap map) =>
        new("default", ".", map.PrimaryLanguage);

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

    // ---- 2026-08-28-5f71: a run with no gate does not read as verified ----

    /// <summary>
    /// p0393's rule, narrowed rather than dropped. Its rationale stands PER REPOSITORY:
    /// a docs or infra repository declaring no commands is skipped, not failed, because
    /// not every repository in a multi-repo run is buildable — and discovery only applies
    /// where the map says .NET. What is narrowed is the RUN. Here the skipped repository
    /// is not the whole run: another one verified something, so the run keeps a second
    /// opinion and stays green.
    /// </summary>
    [Fact]
    public async Task Verify_OneRepositoryVerifiedAnotherSkipped_StaysGreen()
    {
        var (context, sandboxes) = SetupTwo(
            ("server", Map("csharp", new CiConfig(true, "dotnet build", null, null))),
            ("docs", Map("generic", new CiConfig(false, null, null, null))));

        var result = await Handler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("server");
        sandboxes["server"].RanSteps.Should().Contain(s => IsDotnet(s));
        sandboxes["docs"].RanSteps.Should().NotContain(s => IsDotnet(s),
            "a repository nothing verifies is skipped, never guessed at");
    }

    /// <summary>
    /// The other half of the same rule. When the skipped repository IS the whole run,
    /// source was delivered and nobody checked it: the only remaining judge is the
    /// accountant, a model judging the branch, and one party's word is not verification.
    /// </summary>
    [Fact]
    public async Task Verify_SourceDeliveredAndNoCommandResolved_IsNotReportedAsVerified()
    {
        var (context, sandbox) = Setup(Map("generic", new CiConfig(false, null, null, null)));

        var result = await Handler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("UNVERIFIED")
            .And.Contain("server", "the failure names what was searched, per repository")
            .And.Contain("ci.build_command");
        sandbox.RanSteps.Should().NotContain(s => IsDotnet(s),
            "the run fails because nothing verified it, not because something was invented");
    }

    /// <summary>
    /// The run that delivered nothing is untouched by the narrowing: there is nothing for
    /// a gate to have an opinion about, so its silence is not a missing second opinion.
    /// </summary>
    [Fact]
    public async Task Verify_NoSourceDelivered_StaysGreen()
    {
        var (context, sandbox) = Setup(Map("generic", new CiConfig(false, null, null, null)));
        sandbox.GitStatusOutput = string.Empty;

        var result = await Handler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No repository had working-tree changes",
            "nothing was delivered, so nothing is missing a second opinion");
    }

    /// <summary>
    /// A delivery diff that FAILED — no comparable base ref, which is the shallow clone
    /// and the freshly-onboarded repository — has empty text, and empty used to be read as
    /// "delivered nothing", which passes without checking anything. Undetermined is not
    /// unchanged, and the repositories most likely to lack a base ref are exactly the ones
    /// this gate is for.
    /// </summary>
    [Fact]
    public async Task Verify_ARepositoryWhoseDeliveryDiffFailed_IsNotReportedAsUnchanged()
    {
        var (context, sandbox) = Setup(Map("generic", new CiConfig(false, null, null, null)));
        sandbox.DiffFails = true;

        var result = await Handler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("undetermined")
            .And.NotContain("working-tree changes",
                "an unreadable branch is not a branch that changed nothing");
    }

    /// <summary>
    /// WHERE the outcome is applied. The resolution-failure shape returns BEFORE the phase
    /// account is taken, which would strip the run of its per-criterion accounting, skip
    /// the repair pass, and make the run's delivery gate report "no phase measured itself"
    /// — the wrong cause, and the loss of the most actionable thing a run produces.
    /// </summary>
    [Fact]
    public async Task Verify_TheFailingRun_StillCarriesItsPhaseAccount()
    {
        var (context, _) = Setup(
            Map("generic", new CiConfig(false, null, null, null)),
            new PhaseDraft("p1", "goal", "phase: p1", []) { Done = ["the handler is migrated"] });
        context.Pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig("code", new AgentConfig(), "skills", null));

        var result = await Handler(new SatisfiedAccountant()).ExecuteAsync(
            context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("UNVERIFIED").And.Contain("accounted for",
            "the run fails CARRYING its account, not instead of it");
        context.Pipeline.TryGet<IReadOnlyList<SpecAccount>>(
            ContextKeys.PhaseAccounts, out var carried).Should().BeTrue();
        carried.Should().ContainSingle().Which.Criteria.Should().ContainSingle();
        RunAccountLedger.Current(context.Pipeline).All.Should().ContainSingle(
            "the record and the pull request read the account off the ledger");
    }

    private sealed class SatisfiedAccountant : ISpecAccountant
    {
        public Task<SpecAccount> AccountAsync(
            string repoKey, IReadOnlyList<string> criteria, string diff,
            IReadOnlyList<string> commandResults, AgentConfig agent,
            BranchSearch? branchSearch, PipelineCostTracker costTracker,
            CancellationToken cancellationToken,
            int windowBudgetChars = DiffWindows.DefaultBudgetChars) =>
            Task.FromResult(new SpecAccount(repoKey, [.. criteria.Select(c =>
                new CriterionAccount(c, AccountDisposition.Satisfied, "src/Api/Program.cs"))]));
    }

    // ---- 2026-08-31-26d4: a repository declares how it is verified ----

    private static RemoteContextDiscovery Declaring(
        string contextName, string workdir, params ContextYamlVerifyStage[] stages) =>
        new(contextName, workdir, "csharp", Verify: stages);

    /// <summary>
    /// Two contexts resolving the same image collapse into ONE sandbox. The gate reads the
    /// per-sandbox CONTEXT LIST, so BOTH declarations run, each at its own workdir —
    /// reading the sandbox's representative discovery would make whose stages run depend
    /// on discovery order.
    /// </summary>
    [Fact]
    public async Task Verify_TwoContextsCollapsedIntoOneSandbox_RunBothDeclaredLists()
    {
        var (context, sandbox) = Setup(
            Map("csharp", new CiConfig(true, "dotnet build", null, null)),
            contexts:
            [
                Declaring("api", "src/Api", new ContextYamlVerifyStage("lint", "npm run lint")),
                Declaring("web", "src/Web", new ContextYamlVerifyStage("build", "npm run build")),
            ]);

        var result = await Handler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var ran = sandbox.RanSteps.Where(s => CommandLineOf(s).StartsWith("npm")).ToList();
        ran.Select(CommandLineOf).Should().Equal(
            ["npm run lint", "npm run build"],
            "the declaration states the order, and both contexts are in this sandbox");
        ran.Select(s => s.WorkingDirectory).Should().Equal(
            [$"{Repository.SandboxWorkPath}/src/Api", $"{Repository.SandboxWorkPath}/src/Web"],
            "each stage runs at ITS OWN context's workdir");
        sandbox.RanSteps.Should().NotContain(s => IsDotnet(s),
            "the declaration wins over what the analyzer emitted for this run");
    }

    /// <summary>
    /// p0451 filters an INFERRED command that cannot fail and falls through — right for a
    /// framework guess. A declaration is authoritative, so running the rest and reporting
    /// green would be the same false green in new clothes.
    /// </summary>
    [Fact]
    public async Task Verify_ADeclaredCommandThatCannotFail_FailsResolution()
    {
        var (context, sandbox) = Setup(
            Map("csharp", new CiConfig(false, null, null, null)),
            contexts:
            [
                Declaring("api", ".",
                    new ContextYamlVerifyStage("build", "make build"),
                    new ContextYamlVerifyStage("test", "echo tests pass")),
            ]);

        var result = await Handler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("resolution failure")
            .And.Contain("cannot fail").And.Contain("'test'");
        sandbox.RanSteps.Should().NotContain(s => CommandLineOf(s).StartsWith("make"),
            "two of three declared stages green is the false green the rule exists for");
    }

    [Fact]
    public async Task Verify_AStageWhoseConditionPathIsAbsent_IsSkippedAndReported()
    {
        var (context, sandbox) = Setup(
            Map("csharp", new CiConfig(false, null, null, null)),
            contexts:
            [
                Declaring("api", ".",
                    new ContextYamlVerifyStage("bundle", "make bundle", "bundle.yml"),
                    new ContextYamlVerifyStage("test", "make test")),
            ]);
        sandbox.MissingPaths.Add($"{Repository.SandboxWorkPath}/bundle.yml");

        var result = await Handler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("[test]").And.NotContain("bundle",
            "a stage the repository has no files for is skipped, and the outcome names "
            + "only what actually ran");
        sandbox.RanSteps.Should().NotContain(s => CommandLineOf(s) == "make bundle");
        sandbox.RanSteps.Should().Contain(s => CommandLineOf(s) == "make test",
            "an absent condition path skips its own stage, never the ones behind it");
    }

    /// <summary>
    /// The behaviour the predecessor leaves, unchanged: no declaration falls to the
    /// analyzer's inferred pair, then to the .NET entry-point discovery, then to nothing.
    /// </summary>
    [Theory]
    [InlineData("csharp", true, "dotnet build Sample.sln", "[]", "dotnet build Sample.sln")]
    [InlineData("csharp", false, null, "[\"Sample.sln\"]", "Sample.sln")]
    [InlineData("generic", false, null, "[]", null)]
    public async Task Verify_ARepositoryDeclaringNothing_ResolvesInferredThenDotnetThenNothing(
        string language, bool hasCi, string? buildCommand, string listFiles, string? expected)
    {
        var (context, sandbox) = Setup(Map(language, new CiConfig(hasCi, buildCommand, null, null)));
        sandbox.ListFilesJson = listFiles;

        var result = await Handler().ExecuteAsync(context, CancellationToken.None);

        // 2026-08-28-5f71: this test is about WHICH SOURCE names the command. The last case
        // resolves none, and a run that resolved none over a delivery is no longer a success
        // — that verdict belongs to the tests above, so it is not asserted here.
        if (expected is null) sandbox.RanSteps.Should().NotContain(s => IsDotnet(s));
        else
        {
            result.IsSuccess.Should().BeTrue();
            sandbox.RanSteps.Select(CommandLineOf)
                .Should().Contain(line => line.Contains(expected));
        }
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
        /// <summary>2026-08-28-5f71: no comparable base ref — `git diff` exits non-zero and
        /// the delivery is UNDETERMINED rather than empty.</summary>
        public bool DiffFails { get; set; }
        /// <summary>Paths ReadFile answers non-zero for — an absent when_present path.</summary>
        public HashSet<string> MissingPaths { get; } = new(StringComparer.Ordinal);

        public Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            RanSteps.Add(step);
            if (DiffFails && step.Kind == StepKind.Run
                && step.Command == "git" && step.Args!.Contains("diff"))
                return Task.FromResult(new StepResult(
                    StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 128,
                    TimedOut: false, DurationSeconds: 0.01,
                    ErrorMessage: "no comparable base ref", OutputContent: null));
            if (step.Kind == StepKind.ReadFile && MissingPaths.Contains(step.Path ?? ""))
                return Task.FromResult(new StepResult(
                    StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 1,
                    TimedOut: false, DurationSeconds: 0.01, ErrorMessage: "missing", OutputContent: null));
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
