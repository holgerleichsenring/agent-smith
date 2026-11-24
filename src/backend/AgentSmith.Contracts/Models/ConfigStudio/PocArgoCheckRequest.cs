using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>2026-11-23-c81ec: the studio's Test of a PoC target's unsaved argo block — the target's name (the
/// ApplicationSet defaults to 'pocs-&lt;target&gt;') and the block.</summary>
public sealed record PocArgoCheckRequest(string Target, PocArgoConfig Argo);
