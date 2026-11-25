using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using FluentAssertions;

namespace AgentSmith.Tests.Rework.Ci;

/// <summary>
/// 2026-11-17-3435c: a red CI as an act — red on the head of a pull request the newest green attempt left waiting, after
/// its cutoff; or a CI fix that finished and left the red head unmoved. Everything else is no act.
/// </summary>
public sealed class CiActTests
{
    private static readonly DateTimeOffset T = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);
    private const string Url = "https://x/api/pull/1";
    private static readonly ResolvedProject OnCi = new() { Name = "p", Review = ReviewMode.None };

    [Fact]
    public void RedOnHead_AfterTheCutoff_IsAnAct_EvenWhenActed()
    {
        var finding = CiAct.Of(OnCi, Green(), [Record(CiStates.Red, T.AddMinutes(10)) with { CiActed = "abc:red" }]);

        finding!.Act.Should().Match<ReworkAct>(a => a.Channel == ReworkChannel.Ci && a.At == T.AddMinutes(10) && a.Author == "ci");
        finding.Act.Source!.Detail.Should().Be("build");
        finding.Red.Single().Failures.Single().Excerpt.Should().Be("boom");
        finding.FixPushedNothing.Should().BeFalse();
    }

    [Theory]
    [InlineData(CiStates.Pending)]
    [InlineData(CiStates.Green)]
    public void NotRed_IsNoAct(string state) => CiAct.Of(OnCi, Green(), [Record(state, T.AddMinutes(10))]).Should().BeNull();

    [Fact]
    public void RedOnAnOlderHead_BeforeTheCutoff_AFailedAttempt_OrAnotherPolicy_IsNoAct()
    {
        CiAct.Of(OnCi, Green(), [Record(CiStates.Red, T.AddMinutes(10), ciHead: "old")]).Should().BeNull();
        CiAct.Of(OnCi, Green(), [Record(CiStates.Red, T.AddMinutes(-10))]).Should().BeNull();
        CiAct.Of(OnCi, Green() with { Status = RunStatuses.Failed }, [Record(CiStates.Red, T.AddMinutes(10))]).Should().BeNull();
        CiAct.Of(new ResolvedProject { Review = ReviewMode.Each }, Green(),
            [Record(CiStates.Red, T.AddMinutes(10))]).Should().BeNull();
    }

    // 2026-11-25-9992i: the project's review decides, whatever the pull request targets — none acts on a red head, end does not.
    [Fact]
    public void RedIntoASeriesBranch_ActsOnTheProjectsReview()
    {
        var red = Record(CiStates.Red, T.AddMinutes(10)) with { TargetBranch = "agent-smith/series/sr-1" };

        CiAct.Of(OnCi, Green(), [red]).Should().NotBeNull();
        CiAct.Of(new ResolvedProject { Name = "p", Review = ReviewMode.End }, Green(), [red]).Should().BeNull();
    }

    [Fact]
    public void AnOlderAttemptsPullRequest_IsNoAct() =>
        CiAct.Of(OnCi, Green() with { AwaitingCiUrls = new Dictionary<string, string>() }, [Record(CiStates.Red, T.AddMinutes(10))])
            .Should().BeNull();

    [Fact]
    public void ACiFix_ThatLeftTheRedHeadUnmoved_IsAnActAtItsEnd()
    {
        var fix = new PreviousAttempt("fix-1", RunStatuses.Success, T.AddMinutes(20), true)
        {
            CauseChannel = AttemptChannels.Ci, FinishedAt = T.AddMinutes(30),
            PullRequestUrls = new Dictionary<string, string> { ["api"] = Url },
        };

        var finding = CiAct.Of(OnCi, fix, [Record(CiStates.Red, T.AddMinutes(10))]);

        finding!.FixPushedNothing.Should().BeTrue();
        finding.Act.At.Should().Be(T.AddMinutes(30));
        CiAct.Of(OnCi, fix, [Record(CiStates.Red, T.AddMinutes(10)) with { HeadMovedAt = T.AddMinutes(25) }]).Should().BeNull();
    }

    private static PreviousAttempt Green() => new("r1", RunStatuses.Success, T, true)
    {
        AwaitingCiUrls = new Dictionary<string, string> { ["api"] = Url },
        PullRequestUrls = new Dictionary<string, string> { ["api"] = Url },
    };

    private static PrReviewRecordView Record(string state, DateTimeOffset at, string ciHead = "abc") =>
        new(PrKey.Of(Url), Url, "api", "1", "p", "7", "r1", PrReviewStates.Open, T, T, null, [])
        {
            HeadSha = "abc",
            Ci = new PrRecordCiView(state, ciHead, at, state == CiStates.Red ? [new CiFailure("build", "https://ci/1", "boom")] : []),
        };
}
