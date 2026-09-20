using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.DerivationTestLooks;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-20-9c74: the premise check — and no other holder — may RUN one verify stage a
/// repository DECLARED, by label, so a premise about what a gate does is settled by producing
/// its exit code. Every test here drives the FACTORY over a real pipeline carrying the
/// per-sandbox context list: the look builds its own tools in its constructor, so a
/// hand-built look would never carry the tool this phase adds.
/// </summary>
public sealed class VerifyStageRunLookTests
{
    private const string Build = "dotnet build AgentSmith.sln";
    private const string Test = "dotnet test AgentSmith.sln";

    [Fact]
    public void Look_AHolderWhoseTermsForbidIt_HasNoStageRunTool()
    {
        var pipeline = Run(new CountingSandbox(0), Stage("build", Build));

        var forbidden = Factory().OnTerms(
            pipeline, DerivationLookTerms.Derivation, Declared(Stage("build", Build)));
        var allowed = Factory().ForPremiseCheck(pipeline);

        forbidden!.Tools.Select(tool => tool.Name).Should().NotContain(VerifyStageRunTool.Name,
            "the collaborator was supplied, and the TERM still withholds the tool");
        allowed!.Tools.Select(tool => tool.Name).Should().Contain(VerifyStageRunTool.Name);
    }

    [Fact]
    public void Look_TheProposalReview_StillHasNoStageRunTool()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)
            new Dictionary<string, ISandbox> { [Repo] = new CountingSandbox(0) });

        var look = Factory().ForProposalReview(pipeline);

        DerivationLookTerms.ProposalReview.MayRunAStage.Should().BeFalse(
            "it is the cut review's terms with fields replaced, so its value is written out");
        look!.Tools.Select(tool => tool.Name).Should().NotContain(VerifyStageRunTool.Name);
    }

    [Fact]
    public void Look_TheCutReview_HasNoStageRunTool()
    {
        var look = Factory().ForCutReview(Run(new CountingSandbox(0), Stage("build", Build)));

        DerivationLookTerms.CutReview.MayRunAStage.Should().BeFalse();
        look!.Tools.Select(tool => tool.Name).Should().NotContain(VerifyStageRunTool.Name,
            "a cut review runs at derivation time, over a tree the work has not touched");
    }

    [Fact]
    public async Task Look_ThePremiseCheck_ResolvesStagesFromTheSandboxsOwnContexts()
    {
        var sandbox = new CountingSandbox(0);
        var look = Factory().ForPremiseCheck(Run(sandbox, Stage("build", Build)))!;

        var answer = await RunStage(look, Repo, "build");

        sandbox.Ran.Should().ContainSingle().Which.Args.Should().Equal("-c", Build);
        answer.Should().Contain("exited 0");
        look.DeclaredStageLabels(Repo).Should().Equal("build");
    }

    [Fact]
    public async Task Look_AStageThatCannotFail_TakesItsWholeRepositoryOutOfTheList()
    {
        var sandbox = new CountingSandbox(0);
        var look = Factory().ForPremiseCheck(Run(
            sandbox, Stage("build", "echo Build command placeholder"), Stage("test", Test)))!;

        var answer = await RunStage(look, Repo, "test");

        look.DeclaredStageLabels(Repo).Should().BeEmpty(
            "a declared command that cannot fail takes the WHOLE repository out of resolution");
        answer.Should().Contain("no verify stage labelled 'test'");
        sandbox.Ran.Should().BeEmpty("the gate would run neither, so neither is offered");
    }

    [Fact]
    public async Task Look_ALabelTheFilteringRemoved_IsRefusedWithoutChargingTheOneRun()
    {
        var sandbox = new CountingSandbox(0);
        var look = Factory().ForPremiseCheck(Run(
            sandbox, Stage("lint", "npm run lint", whenPresent: "package.json"),
            Stage("build", Build)))!;

        var refused = await RunStage(look, Repo, "lint");
        var ran = await RunStage(look, Repo, "build");

        look.DeclaredStageLabels(Repo).Should().Equal(["lint", "build"],
            "the SENTENCE lists the raw declaration; only a sandbox can apply the presence filter");
        refused.Should().Contain("no verify stage labelled 'lint'");
        ran.Should().Contain("exited 0");
        sandbox.Ran.Should().ContainSingle().Which.Args.Should().Equal("-c", Build);
    }

    [Fact]
    public async Task Look_ARepositoryThatDeclaredNothing_OffersNothingAndNamesNoAnalyzerCommand()
    {
        var sandbox = new CountingSandbox(0);
        var pipeline = Run(sandbox);
        pipeline.Set(ContextKeys.RepoProjectMaps, (IReadOnlyDictionary<string, ProjectMap>)
            new Dictionary<string, ProjectMap> { [Repo] = MapInferring("make all", "make check") });
        var look = Factory().ForPremiseCheck(pipeline)!;

        var answer = await RunStage(look, Repo, "build");

        look.DeclaredStageLabels(Repo).Should().BeEmpty();
        answer.Should().Contain("declares none that can be run here")
            .And.NotContain("make all").And.NotContain("make check");
        sandbox.Ran.Should().BeEmpty(
            "an inferred command is read verbatim out of the analyzer model's JSON — nothing a "
            + "model writes becomes a command");
    }

    [Fact]
    public async Task Look_AStageWhosePresenceConditionFails_IsNotOffered()
    {
        var files = new InMemorySandboxFileReader();
        files.Files["/work/AgentSmith.sln"] = "solution";
        var sandbox = new CountingSandbox(0);
        var look = Factory(new FixedReaderFactory(files)).ForPremiseCheck(Run(
            sandbox, Stage("lint", "npm run lint", whenPresent: "package.json"),
            Stage("build", Build, whenPresent: "AgentSmith.sln")))!;

        var refused = await RunStage(look, Repo, "lint");
        var ran = await RunStage(look, Repo, "build");

        refused.Should().Contain("no verify stage labelled 'lint'").And.Contain("It offers: build");
        ran.Should().Contain("exited 0");
    }

    [Fact]
    public async Task Look_AStageLabelTheRepositoryDidNotDeclare_IsRefusedWithoutRunning()
    {
        var sandbox = new CountingSandbox(0);
        var look = Factory().ForPremiseCheck(Run(sandbox, Stage("build", Build)))!;

        var answer = await RunStage(look, Repo, "deploy");

        answer.Should().Contain("no verify stage labelled 'deploy'").And.Contain("It offers: build");
        sandbox.Ran.Should().BeEmpty();
        look.Evidence.Looks.Should().BeEmpty("nothing ran, so nothing was minted");
    }

    [Fact]
    public async Task Look_AStageRun_UsesTheCommandTheRepositoryDeclared()
    {
        var sandbox = new CountingSandbox(0);
        var look = Factory().ForPremiseCheck(Run(
            sandbox, Stage("build", Build), Stage("test", Test)))!;

        await RunStage(look, Repo, "test");

        var step = sandbox.Ran.Should().ContainSingle().Subject;
        step.Command.Should().Be("/bin/sh");
        step.Args.Should().Equal("-c", Test);
        step.Kind.Should().Be(StepKind.Run);
    }

    [Fact]
    public async Task Look_AStageThatFailed_IsAdmissibleEvidence()
    {
        var look = Factory().ForPremiseCheck(
            Run(new CountingSandbox(exitCode: 1, output: "error CS0103"), Stage("build", Build)))!;

        var answer = await RunStage(look, Repo, "build");

        answer.Should().Contain("exited 1").And.Contain("error CS0103");
        look.Evidence.Looks.Should().ContainSingle().Which.Ran.Should().BeTrue(
            "a gate that failed IS the evidence — the admission needs a look that reached a verdict");
        look.Evidence.Lines.Single().Should().NotContain("could not run");
    }

    [Fact]
    public async Task Look_AStageThatTimedOut_IsNotAdmissible()
    {
        var look = Factory().ForPremiseCheck(
            Run(new TimingOutSandbox("partial output"), Stage("build", Build)))!;

        var answer = await RunStage(look, Repo, "build");

        answer.Should().Contain("timed out").And.Contain("proves nothing");
        look.Evidence.Looks.Should().ContainSingle().Which.Ran.Should().BeFalse();
        look.Evidence.Lines.Single().Should().Contain("could not run, so it proves nothing");
    }

    [Fact]
    public async Task Look_AStageWhoseSandboxThrew_IsNotAdmissibleAndDoesNotPropagate()
    {
        var look = Factory().ForPremiseCheck(
            Run(new ThrowingSandbox(new TimeoutException("no result")), Stage("build", Build)))!;

        var answer = await RunStage(look, Repo, "build");

        answer.Should().Contain("could not be run").And.Contain("TimeoutException")
            .And.Contain("proves nothing");
        look.Evidence.Looks.Should().ContainSingle().Which.Ran.Should().BeFalse(
            "the premise checker turns any escaping exception into a lost check");
    }

    [Fact]
    public async Task Look_ASecondStageRunInOneCheck_IsRefused()
    {
        var sandbox = new CountingSandbox(0);
        var look = Factory().ForPremiseCheck(Run(
            sandbox, Stage("build", Build), Stage("test", Test)))!;

        var first = await RunStage(look, Repo, "build");
        var second = await RunStage(look, Repo, "test");

        first.Should().Contain("exited 0");
        second.Should().Contain("One verify stage may be run per check");
        sandbox.Ran.Should().ContainSingle().Which.Args.Should().Equal("-c", Build);
    }

    [Fact]
    public async Task Look_AFailingStagesOutput_KeepsTheEndNotTheBeginning()
    {
        var output = "HEAD-MARKER" + new string('x', VerifyCommandRunner.OutputTailChars)
            + "TAIL-MARKER";
        var look = Factory().ForPremiseCheck(
            Run(new CountingSandbox(exitCode: 1, output: output), Stage("build", Build)))!;

        var answer = await RunStage(look, Repo, "build");

        answer.Should().Contain("TAIL-MARKER", "a failing build puts its reason at the END")
            .And.NotContain("HEAD-MARKER");
    }

    [Fact]
    public async Task Look_AStageRunsMintedLine_CarriesTheCommandAndTheExitAndNotTheOutput()
    {
        var look = Factory().ForPremiseCheck(
            Run(new CountingSandbox(exitCode: 1, output: "SECRET-OUTPUT"), Stage("build", Build)))!;

        await RunStage(look, Repo, "build");

        var minted = look.Evidence.Looks.Single();
        minted.Kind.Should().Be(EvidenceRecord.VerifyStage);
        minted.Repository.Should().Be(Repo);
        minted.What.Should().Be(Build);
        minted.ExitCode.Should().Be(1);
        minted.Line.Should().Contain(Build).And.Contain("exited 1").And.NotContain("SECRET-OUTPUT",
            "the framework's copy reaches the ticket comment and the decision log");
    }

    [Fact]
    public async Task Look_AStageRunAndARead_DrawOnOneAllowance()
    {
        var sandbox = new CountingSandbox(0);
        var look = Factory().ForPremiseCheck(Run(sandbox, Stage("build", Build)))!;
        var read = new RepositoryFileReadTool(
            look, new FixedReaderFactory(new InMemorySandboxFileReader()), NullLogger.Instance);

        await RunStage(look, Repo, "build");
        string last = string.Empty;
        for (var i = 1; i < DerivationLookTerms.CutReviewAllowance; i++)
            last = await read.ReadFile(Repo, $"src/File{i}.cs");
        var past = await read.ReadFile(Repo, "src/OneTooMany.cs");

        last.Should().NotBe(look.Budget.Exhausted, "the allowance is not spent one look early");
        past.Should().Be(look.Budget.Exhausted,
            "a stage run takes from the SAME allowance a read does");
    }

    [Fact]
    public async Task Look_AStageRun_RunsAtTheVerifyCeilingNotAStepDefault()
    {
        var sandbox = new CountingSandbox(0);
        var look = Factory().ForPremiseCheck(Run(sandbox, Stage("build", Build)))!;

        await RunStage(look, Repo, "build");

        sandbox.Ran.Single().TimeoutSeconds.Should().Be(VerifyCommandRunner.VerifyTimeoutSeconds);
        VerifyCommandRunner.VerifyTimeoutSeconds.Should().Be(1800,
            "a build and a test on a cold sandbox are the slowest deterministic steps in a run");
        sandbox.Ran.Single().TimeoutSeconds.Should().NotBe(60,
            "the step runner's own fallback would time out every stage this tool exists to run");
    }

    [Fact]
    public void Look_TheToolSentence_NamesTheDeclaredLabelsOnlyForAHolderThatMayRunOne()
    {
        var pipeline = Run(new CountingSandbox(0), Stage("build", Build), Stage("test", Test));

        var mayRun = DerivationLookPromptSection.Render(Factory().ForPremiseCheck(pipeline));
        var mayNot = DerivationLookPromptSection.Render(Factory().ForCutReview(pipeline));

        mayRun.Should().Contain("a run of ONE verify stage a repository declared")
            .And.Contain($"- {Repo} (declared verify stages you may run by label: build, test)");
        mayNot.Should().NotContain("verify stage").And.Contain($"- {Repo}");
    }

    private static async Task<string> RunStage(DerivationLook look, string repository, string label)
    {
        var tool = (AIFunction)look.Tools.Single(t => t.Name == VerifyStageRunTool.Name);
        var answer = await tool.InvokeAsync(
            new AIFunctionArguments { ["repository"] = repository, ["label"] = label },
            CancellationToken.None);
        return answer?.ToString() ?? string.Empty;
    }

    private static ContextYamlVerifyStage Stage(
        string label, string command, string? whenPresent = null) =>
        new(label, command, whenPresent);

    private static DerivationLookStages Declared(params ContextYamlVerifyStage[] stages) =>
        new(new VerifyStageResolver(
                new DeclaredStagePresence(
                    new StubSandboxFileReaderFactory(),
                    NullLogger<DeclaredStagePresence>.Instance),
                NullLogger<VerifyStageResolver>.Instance),
            new Dictionary<string, IReadOnlyList<ContextVerifyStages>>
            {
                [Repo] = [new ContextVerifyStages("default", stages)],
            });

    private static ProjectMap MapInferring(string build, string test) =>
        new("csharp", [], [], [], [], new Conventions(null, null, null),
            new CiConfig(HasCi: true, build, test, "github"));

    /// <summary>A run carrying one sandbox and the per-sandbox CONTEXT LIST the declaration is
    /// read from — the map <see cref="ContextVerifyStagesResolver"/> reads, not the grouped
    /// representative.</summary>
    private static PipelineContext Run(ISandbox sandbox, params ContextYamlVerifyStage[] declared)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)
            new Dictionary<string, ISandbox> { [Repo] = sandbox });
        pipeline.Set(ContextKeys.SandboxDiscoveries,
            (IReadOnlyDictionary<string, RemoteContextDiscovery>)
            new Dictionary<string, RemoteContextDiscovery>());
        pipeline.Set(ContextKeys.SandboxContexts,
            (IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>)
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>
            {
                [Repo] = [new RemoteContextDiscovery(
                    "default", ".", "csharp",
                    Verify: declared.Length == 0 ? null : declared)],
            });
        return pipeline;
    }
}
