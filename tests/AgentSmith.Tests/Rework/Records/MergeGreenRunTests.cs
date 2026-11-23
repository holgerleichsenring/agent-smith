using AgentSmith.Application.Models;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Rework;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>2026-11-23-31dce: a merge-green run under the ticket's lease — a lease another run holds is a no and starts
/// nothing; a run that ends before its merge step leaves the outcome slot unset and gives the lease back.</summary>
public sealed class MergeGreenRunTests
{
    private readonly Mock<IActiveRunLease> _leases = new();
    private readonly ResolvedProject _project = new() { Name = "alpha" };
    private readonly CiGreenMergeRequest _request = new("42", "attempt-1", "agent-smith/42", [new CiGreenPullRequest("a", "https://x/a/pull/1")])
    {
        AskedBy = "Dana",
    };

    [Fact]
    public async Task RunAsync_ALeaseAnotherRunHolds_IsANo_AndStartsNothing()
    {
        _leases.Setup(l => l.TryClaimAsync("alpha", new TicketId("42"), It.IsAny<CancellationToken>())).ReturnsAsync(LeaseClaimOutcome.AlreadyClaimed);
        var slot = new ActSlot<IReadOnlyList<PullRequestMergeLine>>();

        var ran = await Run().RunAsync(_project, _request, default, slot);

        ran.Should().BeFalse();
        slot.IsSet.Should().BeFalse();
        _leases.Verify(l => l.ReleaseAsync(It.IsAny<string>(), It.IsAny<TicketId>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_ARunThatCannotStart_LeavesTheSlotUnset_AndReleasesTheLease()
    {
        _leases.Setup(l => l.TryClaimAsync("alpha", new TicketId("42"), It.IsAny<CancellationToken>())).ReturnsAsync(LeaseClaimOutcome.Claimed);
        var slot = new ActSlot<IReadOnlyList<PullRequestMergeLine>>();

        var ran = await Run().RunAsync(_project, _request, default, slot);

        ran.Should().BeTrue("the lease was taken; the run's start failed");
        slot.IsSet.Should().BeFalse();
        _leases.Verify(l => l.ReleaseAsync("alpha", new TicketId("42"), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // No pipeline executor is registered, so the start throws — the path a run that ends before its step takes.
    private MergeGreenRun Run() => new(new ServiceCollection().BuildServiceProvider(), new ServerContext("config.yml"), _leases.Object,
        NullLogger<MergeGreenRun>.Instance);
}
