using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-17-042ed: a phase or epic proposal is reviewed inside the turn that produced it, and
/// the findings come back ON the proposal the turn returns.
/// <para>
/// The review is a call the framework makes on its own behalf, so it is answered by
/// <see cref="HarnessSpecCutReviewer"/> rather than from the master's script — the same boundary
/// p0422 drew for the derivation's cut review, and for the same reason.
/// </para>
/// </summary>
public sealed partial class SpecDialogOutcomeTests
{
    [Fact]
    public async Task Turn_PhaseProposal_IsReviewedBeforeItReturns()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        var reviewer = Reviewer(harness).Finds(new CutFinding(
            "p9999", "GET /widget returns the widget", "contradiction",
            "the phase also forbids touching the service"));
        harness.ChatClient.EnqueueText($"Here is the phase draft:\n{ValidDraft}");

        var result = await RunTurnAsync(harness, Discussed("draft the widget phase now"));

        var asked = reviewer.Asked.Should().ContainSingle().Subject;
        asked.Drafts.Should().ContainSingle().Which.PhaseId.Should().Be("p9999");
        asked.TicketText.Should().BeNull("a design turn has no ticket behind it");
        asked.Repositories.Should().Equal([Repo], "the review reads the turn's own repositories");
        var finding = result.Outcome.Should().BeOfType<PhaseOutcome>()
            .Which.Findings.Should().ContainSingle().Subject;
        finding.Why.Should().Be("the phase also forbids touching the service");
        finding.Quote.Should().Be("GET /widget returns the widget");
        finding.Evidence.Should().BeNull("a contradiction quotes the phase; it cites no look");
        harness.ChatClient.InvocationCount.Should().Be(1, "the review is not drawn from the master's script");
    }

    [Fact]
    public async Task Turn_EpicProposal_IsReviewedAsItsParentAndEveryChild()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        var reviewer = Reviewer(harness);
        harness.ChatClient.EnqueueText(EpicOutcomeReply);

        var result = await RunTurnAsync(harness, Discussed("plan the widget platform"));

        reviewer.Asked.Should().ContainSingle().Which.Drafts.Select(d => d.PhaseId)
            .Should().Equal("p9000", "p9000a", "p9000b");
        result.Outcome.Should().BeOfType<EpicOutcome>().Which.Findings.Should().BeEmpty(
            "a clean review leaves the proposal exactly as the turn wrote it");
    }

    [Fact]
    public async Task Turn_AnswerOrBugOutcome_IsNotReviewed()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        var reviewer = Reviewer(harness);
        harness.ChatClient.EnqueueText(BugOutcomeReply);

        var result = await RunTurnAsync(harness, Discussed("add a null check to AppendTurnAsync"));

        result.Outcome.Should().BeOfType<BugOutcome>();
        reviewer.Asked.Should().BeEmpty("a bug is a ticket someone will run, not a plan to check");
    }

    [Fact]
    public async Task Turn_RefusedThenStrippedProposal_IsNotReviewed()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        var reviewer = Reviewer(harness);
        harness.ChatClient.EnqueueText($"Draft:\n{ValidDraft}").EnqueueText($"Draft again:\n{ValidDraft}");

        var result = await RunTurnAsync(harness, State("update the widgets"));

        result.Outcome.Should().BeOfType<AnswerOutcome>();
        reviewer.Asked.Should().BeEmpty("the refusal stripped the draft, so there is no proposal to review");
    }

    // 2026-09-17-042ed: the stand-in answers what it was ASKED about. A design session proposes
    // several phases across its turns, and findings that followed every later review would report
    // one turn's fault against a phase nobody reviewed — a case would then pass on a lie.
    [Fact]
    public async Task Harness_AFindingOnOnePhase_DoesNotFollowAReviewOfAnother()
    {
        var reviewer = new HarnessSpecCutReviewer().Finds(
            new CutFinding("p9999", "GET /widget returns the widget", "contradiction", "it also forbids it"));
        var tracker = AgentSmith.Application.Services.PipelineCostTracker.GetOrCreate(
            new AgentSmith.Contracts.Commands.PipelineContext());

        var asked = await reviewer.ReviewAsync(
            [new PhaseDraft("p9999", "the widget phase", "spec: p9999", [])], "k", null, null,
            new AgentSmith.Contracts.Models.Configuration.AgentConfig(), tracker, CancellationToken.None);
        var other = await reviewer.ReviewAsync(
            [new PhaseDraft("p8888", "another phase", "spec: p8888", [])], "k", null, null,
            new AgentSmith.Contracts.Models.Configuration.AgentConfig(), tracker, CancellationToken.None);

        asked.Findings.Should().ContainSingle().Which.PhaseId.Should().Be("p9999");
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
            .EnqueueText($"Here is the phase draft:\n{CitingDraft}");

        var result = await RunTurnAsync(harness, Discussed("draft the widget phase now"));

        var finding = result.Outcome.Should().BeOfType<PhaseOutcome>()
            .Which.Findings.Should().ContainSingle().Subject;
        finding.Problem.Should().Be(AgentSmith.Application.Services.SpecDialog.ProposalEvidenceReview.Problem);
        finding.Quote.Should().Be("the widget handler already exists");
        finding.Why.Should().Be($"not a file (missing, or a directory): {Repo}/src/Gone.cs");
        finding.Evidence.Should().BeNull();
    }

    private const string CitingDraft =
        $$"""
        ```yaml
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
        ```
        """;

    private static HarnessSpecCutReviewer Reviewer(RealCompositionHarness harness) =>
        harness.Services.GetRequiredService<HarnessSpecCutReviewer>();
}
