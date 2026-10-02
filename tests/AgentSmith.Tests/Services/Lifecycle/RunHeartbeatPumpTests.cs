using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Lifecycle;

/// <summary>
/// 2026-10-02-5f89e: one pump, two records. The run row beats for every run; the lease beats
/// only for a ticket run, whose claim it guards. A ticketless init used to get neither.
/// </summary>
public sealed class RunHeartbeatPumpTests
{
    private readonly Mock<IActiveRunLease> _lease = new();
    private readonly Mock<IRunHeartbeat> _heartbeat = new();

    private RunHeartbeatPump NewPump() =>
        new(_lease.Object, _heartbeat.Object, TimeProvider.System, NullLogger<RunHeartbeatPump>.Instance);

    [Fact]
    public async Task RunHeartbeatPump_TicketlessRun_RenewsTheRowHeartbeat()
    {
        await NewPump().BeatAsync("p1", ticketId: null, "run-init");

        _heartbeat.Verify(h => h.RenewAsync("run-init", It.IsAny<CancellationToken>()), Times.Once);
        _lease.Verify(l => l.RenewHeartbeatAsync(
            It.IsAny<string>(), It.IsAny<TicketId>(), It.IsAny<CancellationToken>()), Times.Never,
            "a ticketless run holds no lease");
    }

    [Fact]
    public async Task RunHeartbeatPump_TicketRun_RenewsTheRowAndTheLease()
    {
        await NewPump().BeatAsync("p1", new TicketId("42"), "run-1");

        _heartbeat.Verify(h => h.RenewAsync("run-1", It.IsAny<CancellationToken>()), Times.Once);
        _lease.Verify(l => l.RenewHeartbeatAsync("p1", new TicketId("42"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunHeartbeatPump_RowRenewalThrows_TheLeaseIsStillRenewed()
    {
        _heartbeat.Setup(h => h.RenewAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database is locked"));

        await NewPump().BeatAsync("p1", new TicketId("42"), "run-1");

        _lease.Verify(l => l.RenewHeartbeatAsync("p1", new TicketId("42"), It.IsAny<CancellationToken>()), Times.Once,
            "one record's transient fault must not starve the other");
    }

    [Fact]
    public async Task RunHeartbeatPump_RunEnds_ThePumpStopsWithoutABeat()
    {
        using var cts = new CancellationTokenSource();
        var pump = NewPump().RunAsync("p1", null, "run-1", cts.Token);

        cts.Cancel();
        await pump;

        _heartbeat.VerifyNoOtherCalls();
    }
}
