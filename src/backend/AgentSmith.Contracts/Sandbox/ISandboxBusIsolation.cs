namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-11-10-4778: whether this installation's sandboxes share one bus credential. On a shared credential a
/// shell in one sandbox could reach other jobs' channels, so a design turn gets no shell there. A composition
/// that cannot say reports shared.
/// </summary>
public interface ISandboxBusIsolation
{
    bool IsShared { get; }
}
