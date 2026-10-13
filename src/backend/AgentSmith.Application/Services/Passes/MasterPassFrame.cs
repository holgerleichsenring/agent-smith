namespace AgentSmith.Application.Services.Passes;

/// <summary>
/// 2026-10-13-9a59f: one open master pass — what it is and why it opened — holding the ambient pass
/// frame. Dispose it in the method that opened it, once the pass's work is done; ending the pass is the
/// recorder's, and needs only these facts.
/// </summary>
public sealed class MasterPassFrame(
    string runId, string specId, int ordinal, int passInStep, string openedBecause, IDisposable scope)
    : IDisposable
{
    public string RunId { get; } = runId;
    public string SpecId { get; } = specId;

    /// <summary>Run-wide, 1-based; survives a park and a repair.</summary>
    public int Ordinal { get; } = ordinal;

    /// <summary>The pass within its master step: 0 is the step's first loop.</summary>
    public int PassInStep { get; } = passInStep;

    public string OpenedBecause { get; } = openedBecause;

    public void Dispose() => scope.Dispose();
}
