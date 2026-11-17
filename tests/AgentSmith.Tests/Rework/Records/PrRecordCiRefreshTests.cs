using AgentSmith.Contracts.Reviews;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>
/// 2026-11-17-3435a: the refresh reads an open pull request's CI on the head its state read saw and stores it; a head
/// already stored red is read again without its logs and keeps its evidence and the time it was first seen red; a CI
/// read the host refuses leaves the stored CI as it was.
/// </summary>
public sealed class PrRecordCiRefreshTests : IDisposable
{
    private readonly PrReviewRefreshHarness _h = new();
    private readonly List<CiReadRequest> _requests = [];

    public PrRecordCiRefreshTests() =>
        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(PrReviewRefreshHarness.Pr1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrReviewState("1", PrReviewStates.Open, PrReviewRefreshHarness.T, [], "abc"));

    [Fact]
    public async Task OpenPullRequest_StoresItsCi()
    {
        Ci(new PullRequestCi(CiStates.Red, "abc", null, [new CiFailure("build", "https://ci/1", "Assert.Equal failed")]));
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);

        await _h.RefreshAsync();

        var record = _h.Records().Single();
        record.CiState.Should().Be(CiStates.Red);
        record.CiHeadSha.Should().Be("abc");
        record.CiFailuresJson.Should().Contain("Assert.Equal failed");
        _requests.Single().Should().Be(new CiReadRequest("abc", 4000, 5), "the head the state read saw, bounded by the merge block");
    }

    [Fact]
    public async Task RedHeadAgain_SkipsTheLogs_KeepsEvidenceAndTime()
    {
        Ci(new PullRequestCi(CiStates.Red, "abc", null, [new CiFailure("build", "https://ci/1", "Assert.Equal failed")]));
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await _h.RefreshAsync();
        var firstSeen = _h.Records().Single().CiAtTicks;
        Ci(new PullRequestCi(CiStates.Red, "abc", null, [new CiFailure("build", "https://ci/1", "")]));
        _h.Clock.Now = _h.Clock.Now.AddMinutes(5);

        await _h.RefreshAsync(PrReviewRefreshHarness.Pr1);

        _requests[^1].FailuresRead.Should().Be(0);
        var record = _h.Records().Single();
        record.CiAtTicks.Should().Be(firstSeen);
        record.CiFailuresJson.Should().Contain("Assert.Equal failed");
    }

    [Fact]
    public async Task CiReadRefused_KeepsTheStoredCi()
    {
        Ci(new PullRequestCi(CiStates.Pending, "abc", null, []));
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await _h.RefreshAsync();
        _h.Provider.As<IPullRequestCiReader>().Setup(c => c.ReadCiAsync(It.IsAny<string>(), It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("403"));
        _h.Clock.Now = _h.Clock.Now.AddMinutes(5);

        await _h.RefreshAsync(PrReviewRefreshHarness.Pr1);

        var record = _h.Records().Single();
        record.State.Should().Be(PrReviewStates.Open);
        record.CiState.Should().Be(CiStates.Pending);
    }

    private void Ci(PullRequestCi ci) =>
        _h.Provider.As<IPullRequestCiReader>()
            .Setup(c => c.ReadCiAsync(PrReviewRefreshHarness.Pr1, It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>()))
            .Callback<string, CiReadRequest, CancellationToken>((_, r, _) => _requests.Add(r))
            .ReturnsAsync(ci);

    public void Dispose() => _h.Dispose();
}
