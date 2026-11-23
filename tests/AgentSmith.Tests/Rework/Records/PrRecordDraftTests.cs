using AgentSmith.Contracts.Reviews;
using FluentAssertions;
using Moq;
using static AgentSmith.Tests.Rework.Records.PrReviewRefreshHarness;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>2026-11-23-8d16: the refresh records whether an open pull request is still a draft, as the host reads it, and
/// writes nothing to the host.</summary>
public sealed class PrRecordDraftTests : IDisposable
{
    private readonly PrReviewRefreshHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task Refresh_AnOpenDraft_IsRecordedDraft_AndThenReady()
    {
        await _h.RunAsync("r1", Pr1);
        State(PrReviewStates.Open, draft: true);
        await _h.RefreshAsync();
        _h.Records().Single().IsDraft.Should().BeTrue();

        State(PrReviewStates.Open, draft: false);
        _h.Clock.Now = _h.Clock.Now.AddMinutes(10);
        await _h.RefreshAsync();

        _h.Records().Single().IsDraft.Should().BeFalse();
        _h.Provider.Verify(p => p.MarkPullRequestReadyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void State(string state, bool draft) =>
        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(Pr1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrReviewState("1", state, T, []) { IsDraft = draft });
}
