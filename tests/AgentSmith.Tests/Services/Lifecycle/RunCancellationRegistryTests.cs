using AgentSmith.Application.Services.Lifecycle;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Lifecycle;

public sealed class RunCancellationRegistryTests
{
    [Fact]
    public void RunCancellationRegistry_RegisterThenCancel_TokenObservesCancellation()
    {
        var registry = new RunCancellationRegistry(NullLogger<RunCancellationRegistry>.Instance);
        var token = registry.Register("run-1", CancellationToken.None);

        var cancelled = registry.TryCancel("run-1");

        cancelled.Should().BeTrue();
        token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void RunCancellationRegistry_TryCancelUnknownRunId_ReturnsFalse()
    {
        var registry = new RunCancellationRegistry(NullLogger<RunCancellationRegistry>.Instance);

        registry.TryCancel("missing").Should().BeFalse();
    }

    [Fact]
    public void RunCancellationRegistry_UnregisterReleasesEntry_DoubleCancelSafe()
    {
        var registry = new RunCancellationRegistry(NullLogger<RunCancellationRegistry>.Instance);
        registry.Register("run-1", CancellationToken.None);

        registry.TryCancel("run-1").Should().BeTrue();
        registry.Unregister("run-1");

        registry.TryCancel("run-1").Should().BeFalse();
        registry.Snapshot().Should().BeEmpty();
    }

    [Fact]
    public void RunCancellationRegistry_ParentCancellationFlowsToChild()
    {
        var registry = new RunCancellationRegistry(NullLogger<RunCancellationRegistry>.Instance);
        using var parent = new CancellationTokenSource();
        var child = registry.Register("run-1", parent.Token);

        parent.Cancel();

        child.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void RunCancellationRegistry_Snapshot_ContainsRegisteredEntries()
    {
        var registry = new RunCancellationRegistry(NullLogger<RunCancellationRegistry>.Instance);
        registry.Register("a", CancellationToken.None);
        registry.Register("b", CancellationToken.None);

        var snap = registry.Snapshot();

        snap.Should().HaveCount(2);
        snap.Select(e => e.RunId).Should().BeEquivalentTo(new[] { "a", "b" });
    }

    // p0201: reason overload — the watcher and the operator endpoint pass distinct
    // reasons that downstream consumers read via TryGetReason.
    [Fact]
    public void RunCancellationRegistry_TryCancelWithReason_RecordsReasonInRegistry()
    {
        var registry = new RunCancellationRegistry(NullLogger<RunCancellationRegistry>.Instance);
        registry.Register("run-1", CancellationToken.None);

        var cancelled = registry.TryCancel("run-1", "sandbox-vanished");

        cancelled.Should().BeTrue();
        registry.TryGetReason("run-1", out var reason).Should().BeTrue();
        reason.Should().Be("sandbox-vanished");
    }

    [Fact]
    public void RunCancellationRegistry_TryCancelWithReason_FirstReasonWins()
    {
        var registry = new RunCancellationRegistry(NullLogger<RunCancellationRegistry>.Instance);
        registry.Register("run-1", CancellationToken.None);

        registry.TryCancel("run-1", "sandbox-vanished");
        registry.TryCancel("run-1", "operator");

        registry.TryGetReason("run-1", out var reason).Should().BeTrue();
        reason.Should().Be("sandbox-vanished",
            "the first cancel wins so the trail shows the true terminal cause");
    }

    [Fact]
    public void RunCancellationRegistry_TryCancelDefaultOverload_RecordsOperatorReason()
    {
        var registry = new RunCancellationRegistry(NullLogger<RunCancellationRegistry>.Instance);
        registry.Register("run-1", CancellationToken.None);

        registry.TryCancel("run-1");

        registry.TryGetReason("run-1", out var reason).Should().BeTrue();
        reason.Should().Be("operator");
    }

    [Fact]
    public void RunCancellationRegistry_TryGetReasonUnknownRun_ReturnsFalse()
    {
        var registry = new RunCancellationRegistry(NullLogger<RunCancellationRegistry>.Instance);

        registry.TryGetReason("missing", out var reason).Should().BeFalse();
        reason.Should().BeEmpty();
    }
}
