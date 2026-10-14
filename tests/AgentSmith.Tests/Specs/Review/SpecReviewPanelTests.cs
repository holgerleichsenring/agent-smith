using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
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
}
