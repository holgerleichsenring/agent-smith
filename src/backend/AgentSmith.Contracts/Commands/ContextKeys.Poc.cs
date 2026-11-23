namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-23-933db: what the server seeds into a PoC run — strings, so the request survives the queue. Every
/// PoC run is told its PoC by <see cref="PocId"/>.</summary>
public static partial class ContextKeys
{
    /// <summary>The Poc row's id.</summary>
    public const string PocId = "PocId";

    /// <summary>The PocRevision number the run takes in.</summary>
    public const string PocRevision = "PocRevision";

    /// <summary>The principal of the person who started the import.</summary>
    public const string PocActor = "PocActor";
}
