using System.Text.Json;
using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-042eh: where a remaining finding — and the ABSENCE of a review — is readable:
/// the phase record's body, the phase_review artifact and the pull-request body. Three places
/// because three readers: the person who opens the phase, the reader that wants the answer as
/// data (2026-09-17-042ej), and the reviewer of the pull request who opens neither.
/// </summary>
public sealed class PhaseReviewRecordTests
{
    private const string PhaseId = "2026-09-17-042eh";

    [Fact]
    public void PhaseRecord_RemainingFindings_FollowTheAccount()
    {
        var pipeline = WithPhase();
        pipeline.Set<IReadOnlyList<SpecAccount>>(ContextKeys.PhaseAccounts, [Delivered()]);
        PhaseReviewLedger.Record(pipeline, PhaseReviewReport.Taken([Finding()]));

        var body = PhaseRecordBody.For(Draft(), pipeline);

        body.Should().Contain("What this phase delivers").And.Contain("What the phase review still finds");
        body.IndexOf("What the phase review still finds", StringComparison.Ordinal).Should().BeGreaterThan(
            body.IndexOf("What this phase delivers", StringComparison.Ordinal),
            "the account says what was delivered; the review says what it costs to keep");
        body.Should().Contain("src/Api/Handler.cs:4");
    }

    [Fact]
    public void PhaseRecord_ReviewThatWasNotTaken_SaysSoInsteadOfReadingAsClean()
    {
        var pipeline = WithPhase();
        pipeline.Set<IReadOnlyList<SpecAccount>>(ContextKeys.PhaseAccounts, [Delivered()]);
        PhaseReviewLedger.Record(
            pipeline, PhaseReviewReport.NotTaken("the run's configured cost cap is exhausted"));

        PhaseRecordBody.For(Draft(), pipeline).Should()
            .Contain("not reviewed: the run's configured cost cap is exhausted");
    }

    [Fact]
    public void PhaseRecord_ReviewThatRanAndKeptNothing_IsSilent()
    {
        var pipeline = WithPhase();
        pipeline.Set<IReadOnlyList<SpecAccount>>(ContextKeys.PhaseAccounts, [Delivered()]);
        PhaseReviewLedger.Record(pipeline, PhaseReviewReport.Taken([]));

        PhaseRecordBody.For(Draft(), pipeline).Should().NotContain("phase review still finds",
            "a clean review is the quiet case; only its ABSENCE has to be stated");
    }

    [Fact]
    public async Task PhaseReviewArtifact_CarriesWhetherTheReviewWasTakenAtAll()
    {
        var events = new CapturingEventPublisher();
        var pipeline = WithPhase();
        pipeline.Set(ContextKeys.RunId, "2026-09-17T09-00-00-0001");

        await new PhaseReviewPublisher(events).PublishAsync(
            pipeline, PhaseId, PhaseReviewReport.NotTaken("the review call failed"), default);
        await new PhaseReviewPublisher(events).PublishAsync(
            pipeline, PhaseId, PhaseReviewReport.Taken([Finding()]), default);

        var published = events.Events.OfType<PhaseReviewedEvent>().ToList();
        published.Should().HaveCount(2);
        published[0].PhaseId.Should().Be(PhaseId);
        var skipped = Read(published[0].FindingsJson);
        skipped.Reviewed.Should().BeFalse();
        skipped.Why.Should().Be("the review call failed");
        skipped.Findings.Should().BeEmpty();
        var taken = Read(published[1].FindingsJson);
        taken.Reviewed.Should().BeTrue();
        taken.Why.Should().BeNull();
        taken.Findings.Should().ContainSingle().Which.Path.Should().Be("src/Api/Handler.cs");
        published[1].FindingsJson.Should().Contain("\"repository\"").And.Contain("\"line\":4");
    }

    [Fact]
    public async Task PhaseReviewArtifact_WithoutARunId_PublishesNothing()
    {
        var events = new CapturingEventPublisher();

        await new PhaseReviewPublisher(events).PublishAsync(
            WithPhase(), PhaseId, PhaseReviewReport.Taken([]), default);

        events.Events.Should().BeEmpty("there is no run to file the artifact under");
    }

    [Fact]
    public void PullRequestBody_FindingsOfEveryPhase_AreRendered()
    {
        var pipeline = WithPhase();
        PhaseReviewLedger.Record(pipeline, PhaseReviewReport.Taken([Finding()]));
        pipeline.Set(ContextKeys.PhaseSpec, new PhaseDraft("phase-b", "goal", "phase: b\n", []));
        PhaseReviewLedger.Record(
            pipeline, PhaseReviewReport.Taken([Finding() with { Path = "src/Api/Other.cs" }]));

        var section = PhaseReviewSection.Build(pipeline);

        section.Should().Contain(PhaseId).And.Contain("phase-b");
        section.Should().Contain("src/Api/Handler.cs:4").And.Contain("src/Api/Other.cs:4");
    }

    [Fact]
    public void PullRequestBody_PhaseNobodyReviewed_IsNamedWithItsReason()
    {
        var pipeline = WithPhase();
        PhaseReviewLedger.Record(pipeline, PhaseReviewReport.NotTaken("no sandbox was in the pipeline context"));

        PhaseReviewSection.Build(pipeline).Should()
            .Contain(PhaseId).And.Contain("not reviewed: no sandbox was in the pipeline context");
    }

    [Fact]
    public void PullRequestBody_EveryPhaseReviewedAndClean_RendersNothing()
    {
        var pipeline = WithPhase();
        PhaseReviewLedger.Record(pipeline, PhaseReviewReport.Taken([]));

        PhaseReviewSection.Build(pipeline).Should().BeEmpty(
            "an empty heading reads as a section somebody forgot to fill");
    }

    [Fact]
    public void PullRequestBody_ARevertedFinding_SaysSo()
    {
        var pipeline = WithPhase();
        PhaseReviewLedger.Record(pipeline,
            PhaseReviewReport.Taken([Finding() with { Reverted = "fix pass reverted: build red" }]));

        PhaseReviewSection.Build(pipeline).Should().Contain("fix pass reverted: build red");
    }

    private static PhaseReviewReport Read(string json) =>
        JsonSerializer.Deserialize<PhaseReviewReport>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        })!;

    private static PhaseFinding Finding() =>
        new("api", "src/Api/Handler.cs", 4, "files stay under 120 lines", "this one is 400", "P1");

    private static SpecAccount Delivered() =>
        new("api", [new CriterionAccount(
            "the guard is in place", AccountDisposition.Satisfied, "src/Api/Handler.cs")]);

    private static PhaseDraft Draft() => new(PhaseId, "goal", "phase: " + PhaseId + "\n", []);

    private static PipelineContext WithPhase()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PhaseSpec, Draft());
        return pipeline;
    }

    /// <summary>Keeps every event, so a test can read what the publisher actually emitted.</summary>
    private sealed class CapturingEventPublisher : IEventPublisher
    {
        public List<RunEvent> Events { get; } = [];

        public Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(runEvent);
            return Task.CompletedTask;
        }
    }
}
