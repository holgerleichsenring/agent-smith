using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Contracts;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;
using static AgentSmith.Tests.Rework.Records.PrReviewRefreshHarness;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>
/// 2026-11-17-023fb: the generic landing hook — every subscriber is told of a won landing once, after the wake; a
/// subscriber that throws neither fails the landing nor keeps the next subscriber from hearing of it.
/// </summary>
public sealed class TicketLandedHookTests : IDisposable
{
    private readonly PrReviewRefreshHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task AWonLanding_TellsEverySubscriber_EvenAfterOneThrows()
    {
        var throwing = new Mock<ITicketLandedSubscriber>();
        throwing.Setup(s => s.LandedAsync(It.IsAny<TicketLandingFact>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var heard = new List<TicketLandingFact>();
        var listening = new Mock<ITicketLandedSubscriber>();
        listening.Setup(s => s.LandedAsync(It.IsAny<TicketLandingFact>(), It.IsAny<CancellationToken>()))
            .Callback<TicketLandingFact, CancellationToken>((f, _) => heard.Add(f)).Returns(Task.CompletedTask);
        _h.Subscribers.AddRange([throwing.Object, listening.Object]);
        await SeedMergedAsync();

        (await _h.Recorder().RecordMergedAsync(Project, "7", CancellationToken.None)).Should().BeTrue();
        (await _h.Recorder().RecordMergedAsync(Project, "7", CancellationToken.None)).Should().BeFalse();

        heard.Should().ContainSingle().Which.Kind.Should().Be(TicketLandingKinds.Merged, "only the won insert tells");
        heard[0].PrUrl.Should().Be(Pr1);
    }

    private async Task SeedMergedAsync()
    {
        await using var db = _h.Store.Context();
        db.Add(new PrReviewRecord
        {
            PrKey = PrKey.Of(Pr1), Url = Pr1, Repo = "api", Number = "1", Project = "p", TicketId = "7",
            RunId = "run-1", State = PrReviewStates.Merged, StateAtTicks = T.AddHours(1).UtcTicks, CheckedAtTicks = T.AddHours(3).UtcTicks,
        });
        await db.SaveChangesAsync();
    }
}
