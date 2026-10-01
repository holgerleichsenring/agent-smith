namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-10-01-283dc: the files of one uploaded website, read when its sandbox is first filled.
/// A composition with no durable store has no sets and reads none.
/// </summary>
public interface IReferenceSetReader
{
    Task<IReadOnlyList<ReferenceSetFile>> FilesAsync(
        string sessionId, string setId, CancellationToken cancellationToken);
}
