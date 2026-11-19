using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.PipelineHarness.Llm;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-18-9c1ca: a design turn ends when its master ends. No reviewer reads the draft afterwards and nothing sends
/// the master round again; the draft carries only the mechanical checks' findings (cited files). A review the master
/// wants, it commissions itself.
/// </summary>
public sealed partial class SpecDialogOutcomeTests
{
    [Fact]
    public async Task Turn_SpecProposal_IsNotReviewedOrReDrivenAfterTheMasterEnds()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        var reviewer = Reviewer(harness).Finds(new CutFinding(
            "p9999", "GET /widget returns the widget", "contradiction", "the spec also forbids touching the service"));
        ScriptSpec(harness.ChatClient, "Here is the spec draft.");

        var result = await RunTurnAsync(harness, Discussed("draft the widget spec now"));

        reviewer.Asked.Should().BeEmpty("no reviewer runs on the draft unless the master commissions one");
        harness.ChatClient.ScriptedInvocationCount.Should().Be(2, "the write and the reply; nothing re-drives the master");
        result.Outcome.Should().BeOfType<SpecOutcome>().Which.ReviewRounds.Should().BeEmpty();
        result.Reply.Should().Be("Here is the spec draft.");
    }

    [Fact]
    public async Task Turn_EpicProposal_IsNotReviewed()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        var reviewer = Reviewer(harness);
        ScriptEpic(harness.ChatClient);

        var result = await RunTurnAsync(harness, Discussed("plan the widget platform"));

        reviewer.Asked.Should().BeEmpty();
        result.Outcome.Should().BeOfType<EpicOutcome>().Which.Children.Should().HaveCount(2);
    }

    [Fact]
    public async Task Turn_AnswerOrBugOutcome_IsNotReviewed()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        var reviewer = Reviewer(harness);
        ScriptBug(harness.ChatClient);

        var result = await RunTurnAsync(harness, Discussed("add a null check to AppendTurnAsync"));

        result.Outcome.Should().BeOfType<BugOutcome>();
        reviewer.Asked.Should().BeEmpty();
    }

    // 2026-09-17-042ed: the stand-in answers what it was ASKED about. A design session proposes
    // several specs across its turns, and findings that followed every later review would report
    // one turn's fault against a spec nobody reviewed — a case would then pass on a lie.
    [Fact]
    public async Task Harness_AFindingOnOneSpec_DoesNotFollowAReviewOfAnother()
    {
        var reviewer = new HarnessSpecCutReviewer().Finds(
            new CutFinding("p9999", "GET /widget returns the widget", "contradiction", "it also forbids it"));
        var tracker = AgentSmith.Application.Services.PipelineCostTracker.GetOrCreate(
            new AgentSmith.Contracts.Commands.PipelineContext());

        var asked = await reviewer.ReviewAsync(
            [new SpecDraft("p9999", "the widget spec", "spec: p9999", [])], "k", null, null,
            new AgentSmith.Contracts.Models.Configuration.AgentConfig(), tracker, CancellationToken.None);
        var other = await reviewer.ReviewAsync(
            [new SpecDraft("p8888", "another spec", "spec: p8888", [])], "k", null, null,
            new AgentSmith.Contracts.Models.Configuration.AgentConfig(), tracker, CancellationToken.None);

        asked.Findings.Should().ContainSingle().Which.SpecId.Should().Be("p9999");
        other.Findings.Should().BeEmpty("nothing was said about p8888");
    }

    // 2026-10-02-3f06c: the proposal's cited files are read without a model call, through the turn's
    // own scope — opened here by the master's read, as a real turn opens it. A missing file is a
    // finding; an existing path cited with the repository prefix and no lines is none.
    [Fact]
    public async Task SpecDialogOutcome_ProposalCitingAMissingFile_CarriesTheFinding()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        // Missing under both readings a single repository's path is tried at — stripped of the
        // repository name and as written — because the stub answers every other path with a file.
        harness.StubSandboxFactory!.MissingPaths.UnionWith(["src/Gone.cs", $"{Repo}/src/Gone.cs"]);
        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}/src/Router.cs"}""")
            .EnqueueSpecWrite(CitingDraft).EnqueueText("Here is the spec draft.");

        var result = await RunTurnAsync(harness, Discussed("draft the widget spec now"));

        var finding = result.Outcome.Should().BeOfType<SpecOutcome>()
            .Which.Findings.Should().ContainSingle().Subject;
        finding.Problem.Should().Be(AgentSmith.Application.Services.SpecDialog.ProposalEvidenceReview.Problem);
        finding.Quote.Should().Be("the widget handler already exists");
        finding.Why.Should().Be($"not a file (missing, or a directory): {Repo}/src/Gone.cs");
        finding.Evidence.Should().BeNull();
    }

    private const string CitingDraft =
        $$"""
        spec: p9999
        goal: "Add a widget endpoint to the sample service"
        facts:
          - claim: "the router dispatches every request"
            evidence: "{{Repo}}/src/Router.cs"
          - claim: "the widget handler already exists"
            evidence: "{{Repo}}/src/Gone.cs:12"
        steps:
          - id: impl
            action: "Add the widget endpoint + handler"
        tests:
          - "Widget_Get_ReturnsWidget"
        done:
          - "GET /widget returns the widget"
        """;

    private static HarnessSpecCutReviewer Reviewer(RealCompositionHarness harness) =>
        harness.Services.GetRequiredService<HarnessSpecCutReviewer>();
}
