using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecExecution;
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
/// never fails a spec that verified: a finding earns one pass, a cap that is spent earns a
/// recorded reason, and a reverted pass is not asked a second time.
/// </summary>
public sealed class ReviewSpecDiffHandlerTests
{
    private const string Key = "api";
    private const string SpecId = "2026-09-17-042eh";

    [Fact]
    public async Task SpecReview_ConfiguredCapExhausted_SkipsAndRecordsWhy()
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
        var report = SpecReviewLedger.ForThisSpec(pipeline);
        report.Reviewed.Should().BeFalse(
            "a skipped review recorded as an empty finding list reads exactly like a review "
            + "that read the whole diff and objected to nothing");
        report.Why.Should().Contain("cost cap").And.Contain("is exhausted")
            .And.Contain("cache-weighted tokens spent");
    }

    [Fact]
    public async Task SpecReview_NoCapConfigured_Reviews()
    {
        var factory = new ScriptedChatClientFactory(() => new ChatResponse(
            new ChatMessage(ChatRole.Assistant, "[]")));
        var pipeline = Verified();

        var result = await Handler(factory).ExecuteAsync(Context(pipeline), CancellationToken.None);

        factory.ResponseCounter.Should().ContainSingle("with no cap IsBudgetExhausted is false");
        result.Message.Should().Contain("kept no finding");
        result.InsertNext.Should().BeNull();
        SpecReviewLedger.ForThisSpec(pipeline).Reviewed.Should().BeTrue(
            "this one WAS read, and the record must be able to say so");
    }

    [Fact]
    public async Task ReviewSpecDiff_AfterGreenSync_ReviewerReadsOneDiffPerRepository()
    {
        var factory = new ScriptedChatClientFactory(() => new ChatResponse(
            new ChatMessage(ChatRole.Assistant, "[]")));
        var pipeline = Verified();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal)
            {
                [Key] = new DiffingSandbox(),
                ["api-node"] = new DiffingSandbox(),
            });
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            new Dictionary<string, string>(StringComparer.Ordinal) { [Key] = "api", ["api-node"] = "api" });
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [new RepoConnection { Name = "api" }]);

        var result = await Handler(factory).ExecuteAsync(Context(pipeline), CancellationToken.None);

        result.Message.Should().Contain("read 1 changed file(s)",
            "after a green sync the secondary's diff is its primary's, so the reviewer reads it once");
    }

    [Fact]
    public async Task SpecReview_CallFailed_IsRecordedAsNotReviewedNotAsClean()
    {
        var factory = new ScriptedChatClientFactory(
            () => throw new TimeoutException("the provider timed out"));
        var pipeline = Verified();

        var result = await Handler(factory).ExecuteAsync(Context(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("a review that could not be taken never fails a spec");
        var report = SpecReviewLedger.ForThisSpec(pipeline);
        report.Reviewed.Should().BeFalse(
            "a call that failed and a call that objected to nothing are not the same answer");
        report.Why.Should().Contain("failed");
    }

    [Fact]
    public async Task SpecReview_SpecThatChangedNothing_IsRecordedAsNotReviewed()
    {
        var factory = new ScriptedChatClientFactory();
        var pipeline = Verified();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [Key] = new StubSandbox() });

        var result = await Handler(factory).ExecuteAsync(Context(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        factory.ResponseCounter.Should().BeEmpty("there was no diff to put a question about");
        SpecReviewLedger.ForThisSpec(pipeline).Why.Should()
            .Be("this spec changed no reviewable file");
    }

    [Fact]
    public async Task SpecReview_RevertedFixPass_SkipsTheSecondReview()
    {
        var factory = new ScriptedChatClientFactory();
        var pipeline = Verified();
        SpecReviewLedger.Record(pipeline, SpecReviewReport.Taken([Finding()]));
        pipeline.Set(ContextKeys.SpecReviewReverted, "fix pass reverted: build red");

        var result = await Handler(factory).ExecuteAsync(Context(pipeline), CancellationToken.None);

        factory.ResponseCounter.Should().BeEmpty(
            "the branch carries the state the first review already read");
        result.Message.Should().Contain("already reviewed");
        result.InsertNext.Should().BeNull("the one pass is spent");
        SpecReviewLedger.ForThisSpec(pipeline).Findings.Should().ContainSingle();
    }

    [Fact]
    public async Task SpecReview_GreenFixPass_SecondReviewRecordsAndSplicesNothing()
    {
        var factory = new ScriptedChatClientFactory(() => new ChatResponse(
            new ChatMessage(ChatRole.Assistant, Answer())));
        var pipeline = Verified();
        SpecReviewFixPass.Prepare(pipeline);

        var result = await Handler(factory).ExecuteAsync(Context(pipeline), CancellationToken.None);

        factory.ResponseCounter.Should().ContainSingle("the second review is asked");
        result.InsertNext.Should().BeNull("a second pass would be a carousel");
    }

    [Fact]
    public async Task SpecReview_NoSpecIsCurrent_ReviewsNothing()
    {
        var result = await Handler(new ScriptedChatClientFactory())
            .ExecuteAsync(Context(new PipelineContext()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No spec is current");
    }

    [Fact]
    public async Task ReviewSpecDiffHandler_SetContract_ReachesTheReviewer()
    {
        var factory = new PromptCapture();
        var pipeline = Verified();
        pipeline.Set(ContextKeys.SpecSet, new Contracts.Specs.SpecSet("api-1", [], Contracts.Specs.SpecAccounting.Empty,
            [new Contracts.Specs.SpecRevision(1, "initial derivation", DateTimeOffset.UtcNow)], Contracts.Specs.SpecSource.Derived)
        {
            Contract = new([new Contracts.Specs.SeriesContractName("OrderExported", "event", 87, SpecId)], [], [], []),
        });

        await Handler(factory).ExecuteAsync(Context(pipeline), CancellationToken.None);

        factory.Prompts.Should().ContainSingle().Which.Should().Contain("## The series contract " + SpecId)
            .And.Contain($"- name: event 'OrderExported' = 87, owner {SpecId}");
    }

    private static string Answer() =>
        """[{"repository":"api","path":"src/Api/Handler.cs","line":4,"rule":"r","why":"w","cites":"P1"}]""";

    private static SpecFinding Finding() =>
        new(Key, "src/Api/Handler.cs", 4, "files stay under 120 lines", "this one is 400", "P1");

    private static ReviewSpecDiffContext Context(PipelineContext pipeline) =>
        new(new AgentConfig(), pipeline);

    internal static ReviewSpecDiffHandler Handler(Contracts.Services.IChatClientFactory factory, RatifiedMockReview? mocks = null)
    {
        var targets = new SandboxTargets();
        return new ReviewSpecDiffHandler(
            targets,
            new SpecDiffs(
                new DeliveryDiff(
                    new SandboxBaseLadder(
                        new SandboxBaseBranch(NullLogger<SandboxBaseBranch>.Instance),
                        NullLogger<SandboxBaseLadder>.Instance),
                    new SandboxRunStartCommit(NullLogger<SandboxRunStartCommit>.Instance),
                    NullLogger<DeliveryDiff>.Instance),
                NullLogger<SpecDiffs>.Instance),
            DerivationTestLooks.Factory(),
            new SpecDiffReviewer(
                factory, new AsyncLocalRunContextAccessor(), NullLogger<SpecDiffReviewer>.Instance),
            new SpecReviewPublisher(new NoOpEventPublisher()),
            mocks ?? new RatifiedMockReview(new AgentSmith.Tests.Browser.BrowserRenderFixture().Factory()),
            NullLogger<ReviewSpecDiffHandler>.Instance);
    }

    /// <summary>2026-10-13-8762k: a chat client factory that keeps every prompt and answers an empty finding list.</summary>
    private sealed class PromptCapture : Contracts.Services.IChatClientFactory, IChatClient
    {
        public List<string> Prompts { get; } = [];

        public IChatClient Create(AgentConfig agent, Contracts.Providers.TaskType task, int? maxIterations = null,
            Contracts.Providers.MasterLoopHooks? masterLoopHooks = null) => this;

        public int GetMaxOutputTokens(AgentConfig agent, Contracts.Providers.TaskType task) => 4096;

        public string GetModel(AgentConfig agent, Contracts.Providers.TaskType task) => "stub-model";

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Prompts.Add(string.Join("\n", messages.Select(m => m.Text)));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "[]")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    /// <summary>A sandbox whose spec diff carries one changed file, so the review has
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
        pipeline.Set(ContextKeys.CurrentSpec, new SpecDraft(SpecId, "goal", "spec: x\n", []));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [Key] = new DiffingSandbox() });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries, new Dictionary<string, RemoteContextDiscovery>());
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.SpecStartHeads,
            new Dictionary<string, string>(StringComparer.Ordinal) { [Key] = "spec-start-sha" });
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.VerifiedHeads,
            new Dictionary<string, string>(StringComparer.Ordinal) { [Key] = "verified-sha" });
        return pipeline;
    }
}
