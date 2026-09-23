using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Init;
using AgentSmith.Tests.Spawning;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: the manual init door — the one with an operator waiting at it —
/// releases held sandboxes BEFORE it asks the probe. The corpse sweep was deliberately
/// taken out of this door because running it before answering made the operator wait;
/// the release that replaced its place in the sequence is a force remove with no
/// grace, bounded by what this process holds.
/// </summary>
public sealed class HeldSandboxDoorTests
{
    [Fact]
    public async Task Release_TheManualInitDoor_ReleasesBeforeItProbes()
    {
        var recording = new RecordingHeldSandboxes();
        var admission = Admission(recording, recording.AdmittingProbe());

        await admission.TryAdmitAsync(new ResolvedProject { Name = "p1" }, "fix-bug", "run-1", default);

        recording.Order.Should().Equal(RecordingHeldSandboxes.Released, RecordingHeldSandboxes.Probe);
    }

    [Fact]
    public async Task Release_TheManualInitDoor_ReleasesEvenWhenTheProbeWillDeny()
    {
        var recording = new RecordingHeldSandboxes();
        var admission = Admission(recording, recording.DenyingProbe("namespace quota full"));

        var decision = await admission.TryAdmitAsync(
            new ResolvedProject { Name = "p1" }, "fix-bug", "run-1", default);

        decision.Admitted.Should().BeFalse();
        // probe-release-reprobe would read the cluster controller's stale used figure and deny anyway.
        recording.Order.Should().Equal(RecordingHeldSandboxes.Released, RecordingHeldSandboxes.Probe);
    }

    private static InitRunAdmission Admission(
        IHeldSandboxRegister heldSandboxes, ISandboxCapacityProbe probe) =>
        new(CapacityTestDoubles.StubCalculator(), CapacityTestDoubles.AlwaysReserve(),
            heldSandboxes, probe, NullLogger<InitRunAdmission>.Instance);
}
