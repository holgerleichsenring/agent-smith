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

// p0400: the build gate never guesses a filename. Declared context commands win, then
// the analyzer's inferred pair, then nothing — reported, never invented. 2026-09-03-ee12:
// every stage runs at the workdir the project declared, and no rung branches on a
// language. p0430: a branch carrying no source change skips the gate entirely —
// read from the branch, never from a declaration.
public sealed class VerifyPhaseHandlerTests
{
    private static VerifyPhaseHandler Handler(ISpecAccountant? accountant = null) => new(
        new VerifyStageResolver(
            new DeclaredStagePresence(
                new SandboxFileReaderFactory(), NullLogger<DeclaredStagePresence>.Instance),
            NullLogger<VerifyStageResolver>.Instance),
        new ContextVerifyStagesResolver(),
        new VerifyDerivationDrift(
            new VerifyDerivationDigest(new SandboxFileReaderFactory()),
            NullLogger<VerifyDerivationDrift>.Instance),
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

    // 2026-09-03-7bac: every command runs at the REPOSITORY ROOT, the frame the analyzer
    // and the coding master already work in, and meta.workdir places none of them. ee12
    // had moved them to the declaration on run a06c's evidence ('npm run build' at /work
    // in a repository whose manifests live one directory down); run 5a18 is the same
    // mistake from the other side. Neither directory is knowable without looking, so the
    // party that looked writes its own cd into the command.
    [Fact]
    public async Task VerifyPhase_InferredCommand_ContextDeclaresSubtreeWorkdir_StillRunsAtRepoRoot()
    {
        // 2026-09-03-7bac, run 5a18's shape: the context declares the sub-tree its SOURCE
        // occupies, and the analyzer's command names a sibling of that sub-tree. Placing
        // the command at the declaration made a green delivery red.
        var (context, sandbox) = Setup(
            Map("csharp", new CiConfig(true, null, "dotnet test Sample.Tests.Unit", null)),
            workdir: "Sample.Api");

        await Handler().ExecuteAsync(context, CancellationToken.None);

        var test = sandbox.RanSteps.Single(s => CommandLineOf(s) == "dotnet test Sample.Tests.Unit");
        test.WorkingDirectory.Should().Be(Repository.SandboxWorkPath,
            "meta.workdir says where the source lives, not where a command runs — the "
            + "analyzer wrote this path from the root, which is where it stood");
    }

    [Fact]
    public async Task VerifyPhase_InferredCommand_RunsAtRepoRoot()
    {
        var (context, sandbox) = Setup(
            Map("typescript", new CiConfig(true, "npm run build", null, null)));

        await Handler().ExecuteAsync(context, CancellationToken.None);

        var build = sandbox.RanSteps.Single(s => CommandLineOf(s) == "npm run build");
        build.WorkingDirectory.Should().Be(Repository.SandboxWorkPath,
            "every command runs in the frame it was written in");
    }

    [Fact]
    public async Task VerifyPhase_DotnetRepoWithoutCommand_IsReportedNotDiscovered()
    {
        var (context, sandbox) = Setup(Map("csharp", new CiConfig(false, null, null, null)));
        sandbox.ListFilesJson = """["Sample.sln", "src/Sample.Api.csproj"]""";

        var result = await Handler().ExecuteAsync(context, CancellationToken.None);

        sandbox.RanSteps.Should().NotContain(s => IsDotnet(s),
            "a solution file lying in the tree is not a declaration that it builds this repository");
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("UNVERIFIED",
            "csharp is told what python, rust and lua are already told — nothing named a command");
    }

    // ---- 2026-08-28-5f71: a run with no gate does not read as verified ----

    /// <summary>
    /// p0393's rule, narrowed rather than dropped. Its rationale stands PER REPOSITORY:
    /// a docs or infra repository declaring no commands is skipped, not failed, because
    /// not every repository in a multi-repo run is buildable. What is narrowed is the RUN. Here the skipped repository
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
    /// 2026-09-03-7bac: a DECLARED stage takes its frame from the same rule the inferred
    /// one does. An operator writing a verify block inside a context whose source sits in
    /// a sub-tree is writing against the repository root, because that is where the
    /// commands they copied from their own pipeline were written to run.
    /// </summary>
    [Fact]
    public async Task VerifyPhase_DeclaredStage_RunsAtRepoRoot()
    {
        var (context, sandbox) = Setup(
            Map("csharp", new CiConfig(false, null, null, null)),
            contexts: [Declaring("api", "src/Api",
                new ContextYamlVerifyStage("test", "dotnet test tests/Sample.Tests"))]);

        await Handler().ExecuteAsync(context, CancellationToken.None);

        sandbox.RanSteps.Single(s => CommandLineOf(s) == "dotnet test tests/Sample.Tests")
            .WorkingDirectory.Should().Be(Repository.SandboxWorkPath);
    }

    /// <summary>
    /// 2026-09-03-7bac: the path a stage names as its condition is read from the same
    /// place the stage runs. A when_present resolved against the context's sub-tree while
    /// the command ran at the root would skip a gate the repository actually carries.
    /// </summary>
    [Fact]
    public async Task VerifyPhase_DeclaredStage_WhenPresentPathIsRootRelative()
    {
        var (context, sandbox) = Setup(
            Map("csharp", new CiConfig(false, null, null, null)),
            contexts: [Declaring("api", "src/Api",
                new ContextYamlVerifyStage("test", "dotnet test", WhenPresent: "Sample.sln"))]);
        // The sub-tree copy does not exist; only the root one does. Resolving the
        // condition against the declaration would skip a gate the repository carries.
        sandbox.MissingPaths.Add("/work/src/Api/Sample.sln");

        await Handler().ExecuteAsync(context, CancellationToken.None);

        sandbox.RanSteps.Should().Contain(s => CommandLineOf(s) == "dotnet test",
            "the condition names a path at the root, which is where the command runs");
    }

    /// <summary>
    /// 2026-09-03-7bac: run 5a18 was a command run in the wrong place, and reading it as
    /// one meant digging through the trail — the outcome named everything except where it
    /// stood.
    /// </summary>
    [Fact]
    public async Task VerifyPhase_FailureMessage_NamesTheWorkingDirectory()
    {
        var (context, sandbox) = Setup(
            Map("csharp", new CiConfig(true, "dotnet build", null, null)));
        sandbox.FailingCommand = "dotnet build";

        var result = await Handler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain($"run at {Repository.SandboxWorkPath}");
    }

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
            [Repository.SandboxWorkPath, Repository.SandboxWorkPath],
            "both declarations are honoured, and both run in the frame they were written "
            + "in — the collapse is about WHOSE stages run, not about where");
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
    /// 2026-09-03-ee12: no declaration falls to the analyzer's inferred pair and then to
    /// nothing — the same two rungs for every language. The .NET entry-point search that
    /// used to sit between them is gone; a solution file present in the tree resolves no
    /// command for csharp any more than a Cargo.toml does for rust.
    /// </summary>
    [Theory]
    [InlineData("csharp", true, "dotnet build Sample.sln", "[]", "dotnet build Sample.sln")]
    [InlineData("csharp", false, null, "[\"Sample.sln\"]", null)]
    [InlineData("generic", false, null, "[]", null)]
    public async Task Verify_ARepositoryDeclaringNothing_ResolvesInferredThenNothing(
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
        /// <summary>A shell command line whose run exits non-zero.</summary>
        public string? FailingCommand { get; set; }

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
            if (FailingCommand is not null && step.Kind == StepKind.Run
                && step.Command == "/bin/sh" && step.Args is [_, var line] && line == FailingCommand)
                return Task.FromResult(new StepResult(
                    StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 1,
                    TimedOut: false, DurationSeconds: 0.01,
                    ErrorMessage: "the build is red", OutputContent: null));
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
