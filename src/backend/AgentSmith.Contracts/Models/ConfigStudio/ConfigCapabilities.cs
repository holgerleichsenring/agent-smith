using System.Text.Json;
using System.Text.Json.Serialization;

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

/// <summary>
/// One form field of a typed entity: wire key, display label, requiredness and the SHAPE
/// of its value. p0392: the shape used to be client knowledge — a hardcoded "these keys
/// are lists" set in the dashboard — so a backend field of any other shape could not be
/// declared without editing TypeScript as well. It is declared here now, which is what
/// lets a newly declared field render without a UI change.
/// </summary>
public sealed record CapabilityField(
    string Key, string Label, bool Required, CapabilityFieldKind Kind = CapabilityFieldKind.Text);

/// <summary>The value shape of a <see cref="CapabilityField"/>, as the form must edit it.</summary>
[JsonConverter(typeof(CapabilityFieldKindConverter))]
public enum CapabilityFieldKind
{
    /// <summary>A single string.</summary>
    Text,
    /// <summary>A list of strings (a YAML sequence).</summary>
    List,
    /// <summary>A boolean flag.</summary>
    Bool,
    /// <summary>A string-to-string map (a YAML mapping).</summary>
    Map,
}

/// <summary>
/// p0456: serialises <see cref="CapabilityFieldKind"/> as the lowercase word the client
/// contract declares (text / list / bool / map). Without a naming policy the enum crosses
/// the wire as its .NET member name ("List"), which is the one vocabulary on this payload
/// a client is not told about anywhere: every other value here is an explicit wire name
/// (azure_devops, area_path, the camelCased role keys). A naming policy cannot be passed
/// through [JsonConverter], so it is bound here, on the type it belongs to.
/// </summary>
public sealed class CapabilityFieldKindConverter : JsonStringEnumConverter<CapabilityFieldKind>
{
    public CapabilityFieldKindConverter() : base(JsonNamingPolicy.CamelCase) { }
}

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
