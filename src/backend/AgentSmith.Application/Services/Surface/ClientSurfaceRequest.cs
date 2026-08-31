using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: everything one reading of the client call sites needs — the checkouts
/// to read, the operations to answer in, and the agent whose model does the reading.
/// <para>
/// <paramref name="DefaultKey"/> is the sandbox an unprefixed path lands in, and it is a
/// CONSUMER's checkout: the reading is about the clients, so the checkout it falls back to
/// must be one of them.
/// </para>
/// </summary>
public sealed record ClientSurfaceRequest(
    IReadOnlyDictionary<string, ISandbox> Sandboxes,
    string DefaultKey,
    IReadOnlyDictionary<string, string>? KeyToRepo,
    string RepoPath,
    IReadOnlyList<string> ConsumerRepos,
    IReadOnlyList<ServedOperation> Served,
    AgentConfig Agent);
