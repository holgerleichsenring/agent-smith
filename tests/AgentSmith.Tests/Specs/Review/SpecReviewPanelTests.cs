using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.Specs.Review.ReviewLoopRig;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762g: a round's reviewers — concurrent under max_concurrent, each on a look of its own,
/// answers in reviewer order with an untaken one left out.</summary>
public sealed class SpecReviewPanelTests
{
    [Fact]
    public async Task ReviewPanel_TwoReviewers_RunConcurrentlyUnderMaxConcurrent_OnPerSpecLooks()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["shop"] = new DerivationTestLooks.CountingSandbox(0) });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>());
        var looks = new List<DerivationLook?>();
        var (inFlight, peak) = (0, 0);
        var bothIn = new TaskCompletionSource();
        var reviewer = new Mock<ISpecAttackReviewer>();
        reviewer.Setup(r => r.ReviewAsync(It.IsAny<SpecAttackInput>(), It.IsAny<DerivationLook?>(), It.IsAny<AgentConfig>(),
                It.IsAny<PipelineCostTracker>(), It.IsAny<CancellationToken>()))
            .Returns(async (SpecAttackInput _, DerivationLook? look, AgentConfig _, PipelineCostTracker _, CancellationToken _) =>
            {
                lock (looks) { looks.Add(look); peak = Math.Max(peak, ++inFlight); if (inFlight == 2) bothIn.TrySetResult(); }
                await bothIn.Task;
                lock (looks) inFlight--;
                return Build();
            });

        var answers = await new SpecReviewPanel(reviewer.Object, DerivationTestLooks.Factory()).ReviewAsync(
            pipeline, Input(), new SpecReviewConfig { Reviewers = 3, MaxConcurrent = 2 }, new AgentConfig(), CancellationToken.None);

        answers.Should().HaveCount(3).And.OnlyContain(a => a != null);
        peak.Should().Be(2, "two reviewers ran together and the third waited for a slot");
        looks.Should().HaveCount(3).And.OnlyHaveUniqueItems().And.NotContainNulls();
        looks.Should().OnlyContain(l => l!.Terms.Allowance == DerivationLookTerms.AttackReviewFor(Input().Draft).Allowance);
    }

    [Fact]
    public async Task ReviewPanel_ReviewerNotTaken_IsLeftOut_SiblingsKept()
    {
        var reviewer = new Mock<ISpecAttackReviewer>();
        reviewer.Setup(r => r.ReviewAsync(It.IsAny<SpecAttackInput>(), It.IsAny<DerivationLook?>(), It.IsAny<AgentConfig>(),
                It.IsAny<PipelineCostTracker>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SpecAttackInput input, DerivationLook? _, AgentConfig _, PipelineCostTracker _, CancellationToken _) =>
                input.Reviewer == 1 ? null : new SpecAttackResult(SpecReviewVerdicts.Build, [], [
                    new SpecReviewFinding($"reviewer {input.Reviewer}", "shop/src/A.cs:1 [A1]")]));

        var answers = await new SpecReviewPanel(reviewer.Object, DerivationTestLooks.Factory()).ReviewAsync(
            new PipelineContext(), Input(), new SpecReviewConfig { Reviewers = 3, MaxConcurrent = 1 }, new AgentConfig(),
            CancellationToken.None);

        answers.Select(a => a?.Findings.Single().Text).Should().Equal("reviewer 0", null, "reviewer 2");
        SpecReviewMerge.Merge(answers).Findings.Should().HaveCount(2);
    }

    private static SpecAttackInput Input() =>
        new(CutRevisionFixtures.Draft(), SpecReviewScopes.Spec, 1, 0, null, true, [], 0, 2, []);

    // 2026-11-02-2b26b: with real reviewers both calls held in flight at once shared one cost scope and the second
    // threw "skill calls do not nest"; the mocked reviewer above never touches the tracker.
    [Fact]
    public async Task ReviewAsync_TwoRealReviewersInFlightAtOnce_BothAnswerWithTheirOwnTokens()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["shop"] = new DerivationTestLooks.CountingSandbox(0) });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>());
        var reviewer = new SpecAttackReviewer(new HeldInFlightFactory(2), new AsyncLocalRunContextAccessor(),
            new SpecAttackAdmission(NullLogger<SpecAttackAdmission>.Instance), NullLogger<SpecAttackReviewer>.Instance);

        var answers = await new SpecReviewPanel(reviewer, DerivationTestLooks.Factory()).ReviewAsync(
            pipeline, Input(), new SpecReviewConfig { Reviewers = 2, MaxConcurrent = 2 }, new AgentConfig(), CancellationToken.None);

        answers.Should().HaveCount(2).And.OnlyContain(a => a != null, "neither reviewer may lose its call to the other's scope");
        PipelineCostTracker.GetOrCreate(pipeline).PerSkillBreakdown.Should().HaveCount(2)
            .And.OnlyContain(r => r.InputTokens == 10);
    }

    /// <summary>Answers only once <paramref name="calls"/> requests are in flight, so their scopes overlap.</summary>
    private sealed class HeldInFlightFactory(int calls) : IChatClientFactory
    {
        private readonly TaskCompletionSource _allIn = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly int _calls = calls;
        private int _inFlight;

        public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null, MasterLoopHooks? masterLoopHooks = null) =>
            new Client(this);

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;
        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";

        private sealed class Client(HeldInFlightFactory owner) : IChatClient
        {
            public async Task<ChatResponse> GetResponseAsync(
                IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            {
                if (Interlocked.Increment(ref owner._inFlight) == owner._calls) owner._allIn.TrySetResult();
                await owner._allIn.Task.OrHang("both reviewers in flight at once");
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, """{"verdict": "BUILD", "cuts": [], "findings": []}"""))
                {
                    Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 1 },
                };
            }

            public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
                IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
                throw new NotImplementedException();

            public object? GetService(Type serviceType, object? serviceKey = null) => null;
            public void Dispose() { }
        }
    }
}
