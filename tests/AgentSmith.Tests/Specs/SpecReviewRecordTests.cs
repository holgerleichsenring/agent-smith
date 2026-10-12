using System.Text.Json;
using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-042eh: where a remaining finding — and the ABSENCE of a review — is readable:
/// the spec record's body, the spec_review artifact and the pull-request body. Three places
/// because three readers: the person who opens the spec, the reader that wants the answer as
/// data (2026-09-17-042ej), and the reviewer of the pull request who opens neither.
/// </summary>
public sealed class SpecReviewRecordTests
{
    private const string SpecId = "2026-09-17-042eh";

    [Fact]
    public void SpecRecord_RemainingFindings_FollowTheAccount()
    {
        var pipeline = WithSpec();
        pipeline.Set<IReadOnlyList<SpecAccount>>(ContextKeys.SpecAccounts, [Delivered()]);
        SpecReviewLedger.Record(pipeline, SpecReviewReport.Taken([Finding()]));

        var body = SpecRecordBody.For(Draft(), pipeline);

        body.Should().Contain("What this spec delivers").And.Contain("What the spec review still finds");
        body.IndexOf("What the spec review still finds", StringComparison.Ordinal).Should().BeGreaterThan(
            body.IndexOf("What this spec delivers", StringComparison.Ordinal),
            "the account says what was delivered; the review says what it costs to keep");
        body.Should().Contain("src/Api/Handler.cs:4");
    }

    [Fact]
    public void SpecRecord_ReviewThatWasNotTaken_SaysSoInsteadOfReadingAsClean()
    {
        var pipeline = WithSpec();
        pipeline.Set<IReadOnlyList<SpecAccount>>(ContextKeys.SpecAccounts, [Delivered()]);
        SpecReviewLedger.Record(
            pipeline, SpecReviewReport.NotTaken("the run's configured cost cap is exhausted"));

        SpecRecordBody.For(Draft(), pipeline).Should()
            .Contain("not reviewed: the run's configured cost cap is exhausted");
    }

    [Fact]
    public void SpecRecord_ReviewThatRanAndKeptNothing_IsSilent()
    {
        var pipeline = WithSpec();
        pipeline.Set<IReadOnlyList<SpecAccount>>(ContextKeys.SpecAccounts, [Delivered()]);
        SpecReviewLedger.Record(pipeline, SpecReviewReport.Taken([]));

        SpecRecordBody.For(Draft(), pipeline).Should().NotContain("spec review still finds",
            "a clean review is the quiet case; only its ABSENCE has to be stated");
    }

    [Fact]
    public async Task SpecReviewArtifact_CarriesWhetherTheReviewWasTakenAtAll()
    {
        var events = new CapturingEventPublisher();
        var pipeline = WithSpec();
        pipeline.Set(ContextKeys.RunId, "2026-09-17T09-00-00-0001");

        await new SpecReviewPublisher(events).PublishAsync(
            pipeline, SpecId, SpecReviewReport.NotTaken("the review call failed"), default);
        await new SpecReviewPublisher(events).PublishAsync(
            pipeline, SpecId, SpecReviewReport.Taken([Finding()]), default);

        var published = events.Events.OfType<SpecReviewedEvent>().ToList();
        published.Should().HaveCount(2);
        published[0].SpecId.Should().Be(SpecId);
        var skipped = Read(published[0].ReportJson);
        skipped.Reviewed.Should().BeFalse();
        skipped.Why.Should().Be("the review call failed");
        skipped.Findings.Should().BeEmpty();
        var taken = Read(published[1].ReportJson);
        taken.Reviewed.Should().BeTrue();
        taken.Why.Should().BeNull();
        taken.Findings.Should().ContainSingle().Which.Path.Should().Be("src/Api/Handler.cs");
        published[1].ReportJson.Should().Contain("\"repository\"").And.Contain("\"line\":4");
    }

    [Fact]
    public async Task SpecReviewArtifact_WithoutARunId_PublishesNothing()
    {
        var events = new CapturingEventPublisher();

        await new SpecReviewPublisher(events).PublishAsync(
            WithSpec(), SpecId, SpecReviewReport.Taken([]), default);

        events.Events.Should().BeEmpty("there is no run to file the artifact under");
    }

    [Fact]
    public void PullRequestBody_FindingsOfEverySpec_AreRendered()
    {
        var pipeline = WithSpec();
        SpecReviewLedger.Record(pipeline, SpecReviewReport.Taken([Finding()]));
        pipeline.Set(ContextKeys.CurrentSpec, new SpecDraft("spec-b", "goal", "spec: b\n", []));
        SpecReviewLedger.Record(
            pipeline, SpecReviewReport.Taken([Finding() with { Path = "src/Api/Other.cs" }]));

        var section = SpecReviewSection.Build(pipeline);

        section.Should().Contain(SpecId).And.Contain("spec-b");
        section.Should().Contain("src/Api/Handler.cs:4").And.Contain("src/Api/Other.cs:4");
    }

    [Fact]
    public void PullRequestBody_SpecNobodyReviewed_IsNamedWithItsReason()
    {
        var pipeline = WithSpec();
        SpecReviewLedger.Record(pipeline, SpecReviewReport.NotTaken("no sandbox was in the pipeline context"));

        SpecReviewSection.Build(pipeline).Should()
            .Contain(SpecId).And.Contain("not reviewed: no sandbox was in the pipeline context");
    }

    [Fact]
    public void PullRequestBody_EverySpecReviewedAndClean_RendersNothing()
    {
        var pipeline = WithSpec();
        SpecReviewLedger.Record(pipeline, SpecReviewReport.Taken([]));

        SpecReviewSection.Build(pipeline).Should().BeEmpty(
            "an empty heading reads as a section somebody forgot to fill");
    }

    [Fact]
    public void PullRequestBody_ARevertedFinding_SaysSo()
    {
        var pipeline = WithSpec();
        SpecReviewLedger.Record(pipeline,
            SpecReviewReport.Taken([Finding() with { Reverted = "fix pass reverted: build red" }]));

        SpecReviewSection.Build(pipeline).Should().Contain("fix pass reverted: build red");
    }

    private static SpecReviewReport Read(string json) =>
        JsonSerializer.Deserialize<SpecReviewReport>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        })!;

    private static SpecFinding Finding() =>
        new("api", "src/Api/Handler.cs", 4, "files stay under 120 lines", "this one is 400", "P1");

    private static SpecAccount Delivered() =>
        new("api", [new CriterionAccount(
            "the guard is in place", AccountDisposition.Satisfied, "src/Api/Handler.cs")]);

    private static SpecDraft Draft() => new(SpecId, "goal", "spec: " + SpecId + "\n", []);

    private static PipelineContext WithSpec()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CurrentSpec, Draft());
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
