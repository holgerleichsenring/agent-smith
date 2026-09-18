using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Turns;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Specs;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-17-042ed: the review a design turn takes of its own proposal — which outcomes it reads,
/// what it charges the turn, and what it leaves on the proposal.
/// </summary>
public sealed class SpecDialogProposalReviewTests
{
    private const string Repo = "repo-a";

    [Fact]
    public async Task Turn_PhaseProposal_IsReviewedAgainstTheTurnsRepositories()
    {
        var reviewer = new RecordingReviewer(look =>
        {
            var id = look!.Evidence.Remember(new EvidenceRecord(Repo, EvidenceRecord.Read, "read src/Api.cs", 0, Ran: true));
            return new SpecCutReview(
                [new CutFinding("p9999", string.Empty, SpecCutVerdicts.FalsePremise, "the endpoint is already there", Cites: id)]);
        });
        var pipeline = Turn(new PhaseOutcome(Draft("p9999")));

        await Review(reviewer).ReviewAsync(pipeline, new AgentConfig(), Tracker(pipeline), CancellationToken.None);

        reviewer.Drafts.Should().ContainSingle().Which.PhaseId.Should().Be("p9999");
        reviewer.TicketText.Should().BeNull("a design turn has no ticket behind it");
        reviewer.Look!.Repositories.Should().Equal([Repo]);
        var finding = Outcome(pipeline).Findings.Should().ContainSingle().Subject;
        finding.Problem.Should().Be(SpecCutVerdicts.FalsePremise);
        finding.Evidence.Should().Be($"[P1] {Repo}: the proposal review ran 'read src/Api.cs' exited 0");
    }

    [Fact]
    public async Task Turn_EpicProposal_IsReviewedAsItsParentAndEveryChild()
    {
        var reviewer = new RecordingReviewer(_ => SpecCutReview.Clean);
        var pipeline = Turn(new EpicOutcome(Draft("p9000"), [Draft("p9000a"), Draft("p9000b")]));

        await Review(reviewer).ReviewAsync(pipeline, new AgentConfig(), Tracker(pipeline), CancellationToken.None);

        reviewer.Drafts.Select(d => d.PhaseId).Should().Equal("p9000", "p9000a", "p9000b");
        Outcome(pipeline).Findings.Should().BeEmpty("a clean review leaves the proposal as it was");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Turn_AnswerOrBugOutcome_IsNotReviewed(bool bug)
    {
        var reviewer = new RecordingReviewer(_ => SpecCutReview.Clean);
        var pipeline = Turn(bug
            ? new BugOutcome(new BugTicketDraft("a null check", "it dereferences null", null))
            : new AnswerOutcome());

        await Review(reviewer).ReviewAsync(pipeline, new AgentConfig(), Tracker(pipeline), CancellationToken.None);

        reviewer.Calls.Should().Be(0, "an answer is no plan, and a bug is a ticket someone will run");
    }

    [Fact]
    public async Task Gate_CitedIds_AreResolvedToEvidenceLines()
    {
        var reviewer = new RecordingReviewer(look =>
        {
            look!.Evidence.Remember(new EvidenceRecord(Repo, EvidenceRecord.Search, "grep -E 'MapGet' .", 1, Ran: true));
            return new SpecCutReview(
            [
                new CutFinding("p9999", "goal of p9999", "contradiction", "it forbids what it asks for"),
                new CutFinding("p9999", "goal of p9999", SpecCutVerdicts.FalsePremise, "not so", Cites: "P1"),
                new CutFinding("p9999", "goal of p9999", SpecCutVerdicts.FalsePremise, "nor so", Cites: "P7"),
            ]);
        });
        var pipeline = Turn(new PhaseOutcome(Draft("p9999")));

        await Review(reviewer).ReviewAsync(pipeline, new AgentConfig(), Tracker(pipeline), CancellationToken.None);

        var findings = Outcome(pipeline).Findings;
        findings.Select(f => f.Evidence).Should().Equal(
            [null, $"[P1] {Repo}: the proposal review ran 'grep -E 'MapGet' .' exited 1", null],
            "an id the framework minted resolves to its line; one it did not resolves to nothing");
        findings.Should().OnlyContain(f => f.Quote == "goal of p9999",
            "a finding the operator reads keeps what it quoted either way");
    }

    [Fact]
    public async Task Turn_Review_IsChargedToTheTurnsRun()
    {
        // The tracker the review is handed is the one the TURN spends from, proven by spending
        // on it: a reference passed and never used would charge the turn nothing.
        var reviewer = new RecordingReviewer(_ => SpecCutReview.Clean, spends: true);
        var pipeline = Turn(new PhaseOutcome(Draft("p9999")));
        var tracker = Tracker(pipeline);

        await Review(reviewer).ReviewAsync(pipeline, new AgentConfig(), tracker, CancellationToken.None);

        Tracker(pipeline).PerSkillBreakdown.Should().ContainSingle()
            .Which.SkillName.Should().Be("spec-cut-reviewer");
        reviewer.Key.Should().Be("sess-42", "the turn's dialogue job keys the call the ledger records");
    }

    // 2026-09-17-042ed: an LLM-layer NetworkTimeout surfaces as a TaskCanceledException while the
    // RUN's token is not cancelled. This call sits outside the master handler's own guard, so
    // letting it escape destroys a reply the turn already has — the operator is answered with a
    // failure notice instead of the proposal the review was only advising on.
    [Fact]
    public async Task Flow_ReviewTimedOutOnAnUncancelledToken_DoesNotBlockTheProposal()
    {
        var reviewer = new RecordingReviewer(_ => throw new TaskCanceledException("A task was canceled."));
        var pipeline = Turn(new PhaseOutcome(Draft("p9999")));

        var review = async () => await Review(reviewer)
            .ReviewAsync(pipeline, new AgentConfig(), Tracker(pipeline), CancellationToken.None);

        await review.Should().NotThrowAsync("the run was never cancelled — only the model call gave up");
        Outcome(pipeline).Should().BeOfType<PhaseOutcome>().Which.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task Flow_ReviewOnACancelledRun_Propagates()
    {
        var reviewer = new RecordingReviewer(_ => throw new OperationCanceledException());
        var pipeline = Turn(new PhaseOutcome(Draft("p9999")));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var review = async () => await Review(reviewer)
            .ReviewAsync(pipeline, new AgentConfig(), Tracker(pipeline), cancelled.Token);

        await review.Should().ThrowAsync<OperationCanceledException>(
            "an operator cancel is the run ending, not a review to swallow");
    }

    // A false premise is ADMITTED when ANY id it cites names a look that RAN. Showing the first
    // merely resolvable id would print "could not run, so it proves nothing" underneath a finding
    // the framework kept on a different line.
    [Fact]
    public async Task Gate_AFindingCitingSeveralIds_ShowsTheOneThatRan()
    {
        var reviewer = new RecordingReviewer(look =>
        {
            look!.Evidence.Remember(new EvidenceRecord(Repo, EvidenceRecord.Read, "read src/Gone.cs", -1, Ran: false));
            look.Evidence.Remember(new EvidenceRecord(Repo, EvidenceRecord.Search, "grep -E 'MapGet' .", 0, Ran: true));
            return new SpecCutReview(
                [new CutFinding("p9999", "goal of p9999", SpecCutVerdicts.FalsePremise, "not so", Cites: "P1, P2")]);
        });
        var pipeline = Turn(new PhaseOutcome(Draft("p9999")));

        await Review(reviewer).ReviewAsync(pipeline, new AgentConfig(), Tracker(pipeline), CancellationToken.None);

        Outcome(pipeline).Findings.Should().ContainSingle()
            .Which.Evidence.Should().Be($"[P2] {Repo}: the proposal review ran 'grep -E 'MapGet' .' exited 0");
    }

    [Fact]
    public async Task Flow_ReviewFailed_DoesNotBlockTheProposal()
    {
        var reviewer = new RecordingReviewer(_ => throw new InvalidOperationException("model unreachable"));
        var pipeline = Turn(new PhaseOutcome(Draft("p9999")));

        var review = async () => await Review(reviewer)
            .ReviewAsync(pipeline, new AgentConfig(), Tracker(pipeline), CancellationToken.None);

        await review.Should().NotThrowAsync("a review that could not be taken is not evidence of a fault");
        Outcome(pipeline).Should().BeOfType<PhaseOutcome>().Which.Findings.Should().BeEmpty();
    }

    private static SpecDialogProposalReview Review(
        ISpecCutReviewer reviewer, ITurnActivityObserverAccessor? activity = null) =>
        new(reviewer, DerivationTestLooks.Factory(activity: TurnActivityRecorder.Tools(activity)),
            activity ?? TurnActivityRecorder.Silent(),
            NullLogger<SpecDialogProposalReview>.Instance);

    [Fact]
    public async Task DialogTurn_Review_ReportsReviewingAndItsReads()
    {
        var accessor = TurnActivityRecorder.Silent();
        var recorder = new TurnActivityRecorder();
        using var observing = accessor.Observe(recorder);
        var reviewer = new RecordingReviewer(look =>
        {
            ReadThrough(look!).GetAwaiter().GetResult();
            return SpecCutReview.Clean;
        });
        var pipeline = Turn(new PhaseOutcome(Draft("p9999")));

        await Review(reviewer, accessor)
            .ReviewAsync(pipeline, new AgentConfig(), Tracker(pipeline), CancellationToken.None);

        recorder.Lines.Should().Equal(
            ["reviewing", $"tool read_file {Repo}/src/Api.cs"],
            "the review says it started, and its look says what it read");
    }

    private static Task<object?> ReadThrough(DerivationLook look) =>
        look.Tools.OfType<AIFunction>().Single(t => t.Name == RepositoryFileReadTool.Name)
            .InvokeAsync(
                new AIFunctionArguments { ["repository"] = Repo, ["path"] = "src/Api.cs" },
                CancellationToken.None)
            .AsTask();

    private static PhaseDraft Draft(string phaseId) =>
        new(phaseId, $"goal of {phaseId}", $"phase: {phaseId}", []);

    private static PipelineCostTracker Tracker(PipelineContext pipeline) =>
        PipelineCostTracker.GetOrCreate(pipeline);

    private static OutcomeProposal Outcome(PipelineContext pipeline) =>
        pipeline.Get<OutcomeProposal>(ContextKeys.SpecDialogOutcome);

    private static PipelineContext Turn(OutcomeProposal proposal)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)
            new Dictionary<string, ISandbox> { [Repo] = new DerivationTestLooks.CountingSandbox(0) });
        pipeline.Set(ContextKeys.DialogueJobId, "sess-42");
        pipeline.Set(ContextKeys.SpecDialogOutcome, proposal);
        return pipeline;
    }

    private sealed class RecordingReviewer(
        Func<DerivationLook?, SpecCutReview> answer, bool spends = false) : ISpecCutReviewer
    {
        public int Calls { get; private set; }
        public IReadOnlyList<PhaseDraft> Drafts { get; private set; } = [];
        public string? Key { get; private set; }
        public string? TicketText { get; private set; }
        public DerivationLook? Look { get; private set; }
        public PipelineCostTracker? Tracker { get; private set; }

        public Task<SpecCutReview> ReviewAsync(
            IReadOnlyList<PhaseDraft> drafts, string key, string? ticketText, DerivationLook? look,
            AgentConfig agent, PipelineCostTracker costTracker, CancellationToken cancellationToken)
        {
            Calls++;
            (Drafts, Key, TicketText, Look, Tracker) = (drafts, key, ticketText, look, costTracker);
            // As the real reviewer does: the call is opened on the tracker it was handed.
            if (spends)
                costTracker.BeginCall(
                    "spec-cut-reviewer", "spec-cut-reviewer", SkillExecutionPhase.Plan, key).Dispose();
            return Task.FromResult(answer(look));
        }
    }
}
