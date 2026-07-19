namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// p0345c: the backend-truth descriptor the config studio's forms render from
/// (<c>GET /api/config/capabilities</c>). Every list is DERIVED from code truth —
/// the tracker/repo type enums, the registered chat-client builders, the
/// resolution-strategy enum the trigger builder parses, and the code-defined
/// pipeline presets — so the UI can never drift from what the runtime accepts.
/// </summary>
public sealed record ConfigCapabilities(
    IReadOnlyList<TrackerTypeCapability> TrackerTypes,
    IReadOnlyList<ConnectionTypeCapability> ConnectionTypes,
    IReadOnlyList<string> AgentProviders,
    IReadOnlyList<string> ResolutionStrategies,
    IReadOnlyList<string> Pipelines,
    IReadOnlyList<ModelRoleCapability> Roles);

/// <summary>One form field of a typed entity: wire key, display label, requiredness.</summary>
public sealed record CapabilityField(string Key, string Label, bool Required);

/// <summary>
/// One model-routing role the agent form renders as a fixed row (not free text).
/// <see cref="Key"/> is the wire key the studio agent's <c>models</c> map uses
/// (the reserved <c>coding</c> = the top-level model, plus the TaskType roles).
/// <see cref="Optional"/> roles (reasoning) may be left unset.
/// </summary>
public sealed record ModelRoleCapability(string Key, bool Optional);

/// <summary>The per-type field set a tracker of <see cref="Type"/> needs.</summary>
public sealed record TrackerTypeCapability(string Type, IReadOnlyList<CapabilityField> Fields);

/// <summary>
/// The per-type field set a git-host connection of <see cref="Type"/> needs.
/// <see cref="OrgLabel"/> names what the host calls its org segment
/// (organization / owner / group) — the wire key stays <c>organization</c>.
/// </summary>
public sealed record ConnectionTypeCapability(
    string Type, string OrgLabel, IReadOnlyList<CapabilityField> Fields);
