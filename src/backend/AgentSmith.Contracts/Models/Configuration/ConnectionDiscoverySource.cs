namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-10-02-b540: which store answered a connection's discovery read. Shared is the store every
/// replica reads (Redis on the server; the CLI's own stores are its shared truth). Local is this
/// server's own last-good list on disk, served when the shared store has no key for the
/// connection or cannot be reached.
/// </summary>
public enum ConnectionDiscoverySource
{
    Shared,
    Local,
}
