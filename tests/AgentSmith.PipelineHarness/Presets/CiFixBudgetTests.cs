using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-3435c: the code preset through the real composition on a merge: on_ci_green project whose pull request is
/// red on CI after merge.ci_fix_attempts fix runs: FetchTicket reads the red CI as the run's act, splices the hand-back
/// in, and the run parks with the evidence before any model call or sandbox.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class CiFixBudgetTests
{
    private const string Url = "https://stub.test/primary/pull/1";
    private static readonly DateTimeOffset T = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OverBudget_ParksWithTheEvidence_BeforeTheModel()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<IPreviousAttemptReader>();
            services.AddSingleton(Attempts());
            services.RemoveAll<IPrReviewRecordReader>();
            services.AddSingleton(Records());
        });
        var runner = new PipelineRunner(harness.Services) { MergeOverride = new MergePolicyConfig { When = MergeWhen.OnCiGreen } };
        runner.RequestContext[ContextKeys.ProjectName] = "p";

        await runner.RunAsync(PipelinePresets.CodeName);

        var handback = runner.LastContext!.Get<SpecHandback>(ContextKeys.SpecHandback);
        handback.Case.Should().Be(SpecHandbackCase.CiFixBudgetSpent);
        handback.Reason.Should().Contain("merge.ci_fix_attempts: 3").And.Contain("CalcTests.Add: expected 3, was 4");
        runner.LastContext.Get<ReworkAct>(ContextKeys.ReworkAct).Channel.Should().Be(ReworkChannel.Ci);
        harness.ChatClient.InvocationCount.Should().Be(0, "the run parked before any model call");
    }

    private static IPreviousAttemptReader Attempts()
    {
        var reader = new Mock<IPreviousAttemptReader>();
        reader.Setup(r => r.LatestAsync(It.IsAny<string>(), "1", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("fix-3", RunStatuses.Success, T, true)
            {
                CauseChannel = AttemptChannels.Ci, CiFixStreak = 3, FinishedAt = T.AddMinutes(5),
                AwaitingCiUrls = new Dictionary<string, string> { ["primary"] = Url },
                PullRequestUrls = new Dictionary<string, string> { ["primary"] = Url },
            });
        return reader.Object;
    }

    private static IPrReviewRecordReader Records()
    {
        var reader = new Mock<IPrReviewRecordReader>();
        reader.Setup(r => r.ByTicketAsync(It.IsAny<string>(), "1", It.IsAny<CancellationToken>())).ReturnsAsync([
            new PrReviewRecordView(PrKey.Of(Url), Url, "primary", "1", "p", "1", "fix-3", PrReviewStates.Open, T, T, null, [])
            {
                HeadSha = "abc",
                Ci = new PrRecordCiView(CiStates.Red, "abc", T.AddMinutes(10), [new CiFailure("test", "https://ci/1", "CalcTests.Add: expected 3, was 4")]),
            }]);
        return reader.Object;
    }
}
