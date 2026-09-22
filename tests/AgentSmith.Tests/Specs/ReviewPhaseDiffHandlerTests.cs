using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-042eh: the step itself — what it asks, what it skips, and what it splices. It
/// never fails a phase that verified: a finding earns one pass, a cap that is spent earns a
/// recorded reason, and a reverted pass is not asked a second time.
/// </summary>
public sealed class ReviewPhaseDiffHandlerTests
{
    private const string Key = "api";
    private const string PhaseId = "2026-09-17-042eh";

    [Fact]
    public async Task PhaseReview_ConfiguredCapExhausted_SkipsAndRecordsWhy()
    {
        var factory = new ScriptedChatClientFactory();
        var pipeline = Verified();
        pipeline.Set("PipelineCostCap", new CostCapValues { Usd = -1m, Tokens = -1 });

        var result = await Handler(factory).ExecuteAsync(Context(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // 2026-09-22-7c41a: the refusal NAMES the cap and the spend — a resumed run carries
        // the segment before it, so this reader can stop a run far earlier than its own
        // record suggests, and "exhausted" on its own explains nothing.
        result.Message.Should().Contain("is exhausted").And.Contain("cache-weighted tokens");
        factory.ResponseCounter.Should().BeEmpty("a skipped review costs nothing");
        var report = PhaseReviewLedger.ForThisPhase(pipeline);
        report.Reviewed.Should().BeFalse(
            "a skipped review recorded as an empty finding list reads exactly like a review "
            + "that read the whole diff and objected to nothing");
        report.Why.Should().Contain("cost cap").And.Contain("is exhausted")
            .And.Contain("cache-weighted tokens spent");
    }

    [Fact]
    public async Task PhaseReview_NoCapConfigured_Reviews()
    {
        var factory = new ScriptedChatClientFactory(() => new ChatResponse(
            new ChatMessage(ChatRole.Assistant, "[]")));
        var pipeline = Verified();

        var result = await Handler(factory).ExecuteAsync(Context(pipeline), CancellationToken.None);

        factory.ResponseCounter.Should().ContainSingle("with no cap IsBudgetExhausted is false");
        result.Message.Should().Contain("kept no finding");
        result.InsertNext.Should().BeNull();
        PhaseReviewLedger.ForThisPhase(pipeline).Reviewed.Should().BeTrue(
            "this one WAS read, and the record must be able to say so");
    }

    [Fact]
    public async Task PhaseReview_CallFailed_IsRecordedAsNotReviewedNotAsClean()
    {
        var factory = new ScriptedChatClientFactory(
            () => throw new TimeoutException("the provider timed out"));
        var pipeline = Verified();

        var result = await Handler(factory).ExecuteAsync(Context(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("a review that could not be taken never fails a phase");
        var report = PhaseReviewLedger.ForThisPhase(pipeline);
        report.Reviewed.Should().BeFalse(
            "a call that failed and a call that objected to nothing are not the same answer");
        report.Why.Should().Contain("failed");
    }

    [Fact]
    public async Task PhaseReview_PhaseThatChangedNothing_IsRecordedAsNotReviewed()
    {
        var factory = new ScriptedChatClientFactory();
        var pipeline = Verified();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [Key] = new StubSandbox() });

        var result = await Handler(factory).ExecuteAsync(Context(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        factory.ResponseCounter.Should().BeEmpty("there was no diff to put a question about");
        PhaseReviewLedger.ForThisPhase(pipeline).Why.Should()
            .Be("this phase changed no reviewable file");
    }

    [Fact]
    public async Task PhaseReview_RevertedFixPass_SkipsTheSecondReview()
    {
        var factory = new ScriptedChatClientFactory();
        var pipeline = Verified();
        PhaseReviewLedger.Record(pipeline, PhaseReviewReport.Taken([Finding()]));
        pipeline.Set(ContextKeys.PhaseReviewReverted, "fix pass reverted: build red");

        var result = await Handler(factory).ExecuteAsync(Context(pipeline), CancellationToken.None);

        factory.ResponseCounter.Should().BeEmpty(
            "the branch carries the state the first review already read");
        result.Message.Should().Contain("already reviewed");
        result.InsertNext.Should().BeNull("the one pass is spent");
        PhaseReviewLedger.ForThisPhase(pipeline).Findings.Should().ContainSingle();
    }

    [Fact]
    public async Task PhaseReview_GreenFixPass_SecondReviewRecordsAndSplicesNothing()
    {
        var factory = new ScriptedChatClientFactory(() => new ChatResponse(
            new ChatMessage(ChatRole.Assistant, Answer())));
        var pipeline = Verified();
        PhaseReviewFixPass.Prepare(pipeline);

        var result = await Handler(factory).ExecuteAsync(Context(pipeline), CancellationToken.None);

        factory.ResponseCounter.Should().ContainSingle("the second review is asked");
        result.InsertNext.Should().BeNull("a second pass would be a carousel");
    }

    [Fact]
    public async Task PhaseReview_NoPhaseIsCurrent_ReviewsNothing()
    {
        var result = await Handler(new ScriptedChatClientFactory())
            .ExecuteAsync(Context(new PipelineContext()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No phase is current");
    }

    private static string Answer() =>
        """[{"repository":"api","path":"src/Api/Handler.cs","line":4,"rule":"r","why":"w","cites":"P1"}]""";

    private static PhaseFinding Finding() =>
        new(Key, "src/Api/Handler.cs", 4, "files stay under 120 lines", "this one is 400", "P1");

    private static ReviewPhaseDiffContext Context(PipelineContext pipeline) =>
        new(new AgentConfig(), pipeline);

    private static ReviewPhaseDiffHandler Handler(ScriptedChatClientFactory factory)
    {
        var targets = new SandboxTargets();
        return new ReviewPhaseDiffHandler(
            targets,
            new PhaseDiffs(
                new DeliveryDiff(
                    new SandboxBaseLadder(
                        new SandboxBaseBranch(NullLogger<SandboxBaseBranch>.Instance),
                        NullLogger<SandboxBaseLadder>.Instance),
                    new SandboxRunStartCommit(NullLogger<SandboxRunStartCommit>.Instance),
                    NullLogger<DeliveryDiff>.Instance),
                NullLogger<PhaseDiffs>.Instance),
            DerivationTestLooks.Factory(),
            new PhaseDiffReviewer(
                factory, new AsyncLocalRunContextAccessor(), NullLogger<PhaseDiffReviewer>.Instance),
            new PhaseReviewPublisher(new NoOpEventPublisher()),
            NullLogger<ReviewPhaseDiffHandler>.Instance);
    }

    /// <summary>A sandbox whose phase diff carries one changed file, so the review has
    /// something to read — the stub's diff is driven by staging, which no test here does.</summary>
    private sealed class DiffingSandbox : ISandbox
    {
        public string JobId => "diffing-sandbox";

        public Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken) =>
            Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0, TimedOut: false,
                DurationSeconds: 0.01, ErrorMessage: null,
                OutputContent:
                    "diff --git a/src/Api/Handler.cs b/src/Api/Handler.cs\n"
                    + "--- a/src/Api/Handler.cs\n+++ b/src/Api/Handler.cs\n@@ -1 +1 @@\n+var x = 1;\n"));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>A pipeline standing where a green verification leaves one, with one sandbox
    /// whose diff carries one changed file.</summary>
    private static PipelineContext Verified()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PhaseSpec, new PhaseDraft(PhaseId, "goal", "phase: x\n", []));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [Key] = new DiffingSandbox() });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries, new Dictionary<string, RemoteContextDiscovery>());
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.PhaseStartHeads,
            new Dictionary<string, string>(StringComparer.Ordinal) { [Key] = "phase-start-sha" });
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.VerifiedHeads,
            new Dictionary<string, string>(StringComparer.Ordinal) { [Key] = "verified-sha" });
        return pipeline;
    }
}
