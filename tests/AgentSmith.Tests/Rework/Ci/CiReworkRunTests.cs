using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.Rework.Ci;

/// <summary>
/// 2026-11-17-3435c: a fix run reads the red CI it serves — the failed checks reach the master's prompt and the act the
/// run's slot; at merge.ci_fix_attempts fix runs in a row, or after a fix that pushed nothing, the run hands back with
/// the evidence instead.
/// </summary>
public sealed class CiReworkRunTests
{
    private static readonly DateTimeOffset T = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);
    private const string Url = "https://x/api/pull/1";

    [Fact]
    public async Task UnderBudget_SetsTheEvidenceAndTheAct_AndContinues()
    {
        var pipeline = Pipeline(streak: 2);

        (await Sut().ApplyAsync(Context(pipeline), default)).Should().BeFalse();

        pipeline.Get<ReworkAct>(ContextKeys.ReworkAct).Channel.Should().Be(ReworkChannel.Ci);
        var section = CiFailurePromptSection.Render(pipeline);
        section.Should().Contain(CiFailurePromptSection.Heading).And.Contain("CalcTests.Add failed").And.Contain("https://ci/1");
        pipeline.Has(ContextKeys.SpecHandback).Should().BeFalse();
    }

    [Fact]
    public async Task AtTheBudget_HandsBack_WithTheEvidenceAndTheKey()
    {
        var pipeline = Pipeline(streak: 3);

        (await Sut().ApplyAsync(Context(pipeline), default)).Should().BeTrue();

        var handback = pipeline.Get<SpecHandback>(ContextKeys.SpecHandback);
        handback.Case.Should().Be(SpecHandbackCase.CiFixBudgetSpent);
        handback.Reason.Should().Contain("merge.ci_fix_attempts: 3").And.Contain("CalcTests.Add failed");
    }

    [Fact]
    public async Task APersonsNewerAct_Wins_AndNothingIsHandedBack()
    {
        var pipeline = Pipeline(streak: 5);
        pipeline.Set(ContextKeys.ReworkAct, new ReworkAct("alice", T.AddHours(5)));

        (await Sut().ApplyAsync(Context(pipeline), default)).Should().BeFalse();
        pipeline.Get<ReworkAct>(ContextKeys.ReworkAct).Author.Should().Be("alice");
    }

    private static CiRework Sut()
    {
        var reader = new Mock<IPrReviewRecordReader>();
        reader.Setup(r => r.ByTicketAsync("p", "7", It.IsAny<CancellationToken>())).ReturnsAsync([
            new PrReviewRecordView(PrKey.Of(Url), Url, "api", "1", "p", "7", "r1", PrReviewStates.Open, T, T, null, [])
            {
                HeadSha = "abc",
                Ci = new PrRecordCiView(CiStates.Red, "abc", T.AddMinutes(10), [new CiFailure("test", "https://ci/1", "CalcTests.Add failed")]),
            }]);
        var services = new ServiceCollection().AddSingleton(reader.Object).BuildServiceProvider();
        return new CiRework(services.GetRequiredService<IServiceScopeFactory>());
    }

    private static PipelineContext Pipeline(int streak)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject { Name = "p", Merge = new MergePolicyConfig { When = MergeWhen.OnCiGreen } });
        pipeline.Set(ContextKeys.PreviousAttempt, new PreviousAttempt("r1", RunStatuses.Success, T, true)
        {
            AwaitingCiUrls = new Dictionary<string, string> { ["api"] = Url }, CiFixStreak = streak,
        });
        return pipeline;
    }

    private static FetchTicketContext Context(PipelineContext pipeline) =>
        new(new TicketId("7"), new TrackerConnection(), pipeline);
}
