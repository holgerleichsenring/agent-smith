using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: a register and a probe that write into ONE list, so a door's
/// order is provable — the release runs BEFORE the probe, never after a denial,
/// because a Kubernetes quota's used figure does not drop when a pod is deleted.
/// </summary>
internal sealed class RecordingHeldSandboxes : IHeldSandboxRegister
{
    public const string Released = "release";
    public const string Probe = "probe";

    public List<string> Order { get; } = [];

    public void Hold(HeldSandbox held) => throw new NotSupportedException();

    public bool Take(string key) => throw new NotSupportedException();

    public void Release(string key) => throw new NotSupportedException();

    public Task<int> EvictAsync(CancellationToken cancellationToken)
    {
        Order.Add(Released);
        return Task.FromResult(0);
    }

    public ISandboxCapacityProbe AdmittingProbe() => new OrderedProbe(Order, CapacityDecision.Admit());

    public ISandboxCapacityProbe DenyingProbe(string reason) =>
        new OrderedProbe(Order, CapacityDecision.Deny(reason));

    private sealed class OrderedProbe(List<string> order, CapacityDecision decision) : ISandboxCapacityProbe
    {
        public Task<CapacityDecision> HasCapacityAsync(
            RunFootprint footprint, CancellationToken cancellationToken)
        {
            order.Add(Probe);
            return Task.FromResult(decision);
        }
    }
}
