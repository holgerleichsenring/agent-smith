using System.Reflection;
using System.Runtime.Serialization;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0345c: builds the <see cref="ConfigCapabilities"/> descriptor from code truth
/// and enforces it on writes. Type lists enumerate <see cref="TrackerType"/> /
/// <see cref="RepoType"/> (wire names from their EnumMember attributes — the same
/// names the YAML loader binds), resolution strategies enumerate
/// <see cref="ResolutionStrategy"/> (what EffectiveTriggerBuilder parses),
/// pipelines are <see cref="PipelinePresets.Names"/>, and agent providers are the
/// registered chat-client builders' supported types (passed in by the host). The
/// per-type FIELD descriptors here are the single source for both the rendered
/// form and the upsert validation, so they cannot drift apart; an unmapped enum
/// value throws, and the coverage test turns that into a build-time tripwire.
/// </summary>
public static class ConfigStudioCapabilities
{
    public static ConfigCapabilities Build(IEnumerable<string> agentProviders) => new(
        TrackerTypes: Enum.GetValues<TrackerType>()
            .Select(t => new TrackerTypeCapability(WireName(t), TrackerCapabilityFields.For(t))).ToList(),
        ConnectionTypes: Enum.GetValues<RepoType>()
            .Where(t => t != RepoType.Local) // Local is a repo locator, not a discoverable git host.
            .Select(t => new ConnectionTypeCapability(WireName(t), OrgLabel(t), ConnectionFields(t))).ToList(),
        AgentProviders: agentProviders.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.Ordinal).ToList(),
        ResolutionStrategies: ResolutionStrategyNames,
        Pipelines: PipelinePresets.Names,
        Roles: RoleCapabilities);

    /// <summary>
    /// The fixed model-role set the agent form renders — every <see cref="TaskType"/> role,
    /// primary first and required; the others optional: an unset role inherits (from
    /// primary, code-map from scout first). Keys are camelCased TaskType names.
    /// </summary>
    public static IReadOnlyList<ModelRoleCapability> RoleCapabilities { get; } =
        [.. ModelRoleSlots.All.Select(s => new ModelRoleCapability(
            s.Key, Optional: s.Task != TaskType.Primary, NeedsStrong: s.NeedsStrong))];

    /// <summary>The valid model-role keys (the TaskType roles).</summary>
    public static IReadOnlyList<string> RoleKeys { get; } = ModelRoleSlots.Keys;

    /// <summary>The wire names of every known resolution strategy (tag / area_path / repo / to_address).</summary>
    public static IReadOnlyList<string> ResolutionStrategyNames { get; } =
        Enum.GetValues<ResolutionStrategy>().Select(WireName).ToList();

    /// <summary>Known tracker type wire names (github / gitlab / azure_devops / jira).</summary>
    public static IReadOnlyList<string> TrackerTypeNames { get; } =
        Enum.GetValues<TrackerType>().Select(WireName).ToList();

    // ---- write-side enforcement (shared by every IConfigStore implementation) ----

    /// <summary>
    /// Rejects a tracker whose type is unknown or that misses a field the
    /// capabilities descriptor declares required for that type.
    /// </summary>
    public static void ValidateTracker(TrackerEntity tracker)
    {
        var missing = MissingTrackerFields(tracker).Select(f => f.Key).ToList();
        if (missing.Count > 0)
            throw new ConfigurationException(
                $"Tracker '{tracker.Id}' (type {tracker.Type}): missing required field(s) " +
                $"{string.Join(", ", missing)}.");
    }

    /// <summary>
    /// 2026-10-06-cea8: the required fields this tracker leaves empty, each on its own, so a
    /// draft check can name every one where it lives. Throws for an unknown type.
    /// </summary>
    public static IReadOnlyList<CapabilityField> MissingTrackerFields(TrackerEntity tracker)
    {
        var type = Enum.GetValues<TrackerType>()
            .Where(t => WireName(t).Equals(tracker.Type, StringComparison.OrdinalIgnoreCase))
            .Cast<TrackerType?>().FirstOrDefault()
            ?? throw new ConfigurationException(
                $"Tracker '{tracker.Id}': unknown type '{tracker.Type}' " +
                $"(known: {string.Join(", ", TrackerTypeNames)}).");
        return TrackerCapabilityFields.For(type)
            .Where(f => f.Required && string.IsNullOrWhiteSpace(FieldValue(tracker, f.Key)))
            .ToList();
    }

    /// <summary>
    /// Rejects a project whose resolution names a strategy the trigger builder
    /// does not parse, or that carries an empty match value.
    /// </summary>
    public static void ValidateProjectResolution(ProjectEntity project)
    {
        if (project.Resolution is not { } resolution) return;
        if (!ResolutionStrategyNames.Contains(resolution.Strategy, StringComparer.OrdinalIgnoreCase))
            throw new ConfigurationException(
                $"Project '{project.Id}': resolution strategy '{resolution.Strategy}' is not a known " +
                $"strategy ({string.Join("/", ResolutionStrategyNames)}).");
        if (string.IsNullOrWhiteSpace(resolution.Value))
            throw new ConfigurationException(
                $"Project '{project.Id}': resolution value must not be empty.");
    }

    // ---- per-type field descriptors (rendered by the form, enforced above) ----

    private static IReadOnlyList<CapabilityField> ConnectionFields(RepoType type) => type switch
    {
        // Grounded in RawConnectionEntry + the connection patcher: ADO needs
        // organization + team project, GitHub an owner, GitLab a group.
        RepoType.AzureDevOps =>
        [
            new CapabilityField("organization", "Organization", Required: true),
            new CapabilityField("project", "Project", Required: true),
            new CapabilityField("authSecret", "Auth secret", Required: true, CapabilityFieldKind.Secret),
            new CapabilityField("host", "Base URL", Required: false),
            new CapabilityField("defaultBranch", "Default branch", Required: false),
        ],
        RepoType.GitHub or RepoType.GitLab =>
        [
            new CapabilityField("organization", type == RepoType.GitHub ? "Owner" : "Group", Required: true),
            new CapabilityField("authSecret", "Auth secret", Required: true, CapabilityFieldKind.Secret),
            // p0392: ConnectionRepoUrlBuilder reads host on every type, so a self-hosted
            // GitLab or GitHub Enterprise was configurable in YAML and nowhere else.
            new CapabilityField("host", "Base URL", Required: false),
            new CapabilityField("defaultBranch", "Default branch", Required: false),
        ],
        _ => throw new ConfigurationException(
            $"Connection type '{type}' has no capabilities descriptor — add its field set."),
    };

    private static string OrgLabel(RepoType type) => type switch
    {
        RepoType.AzureDevOps => "organization",
        RepoType.GitHub => "owner",
        RepoType.GitLab => "group",
        _ => throw new ConfigurationException($"Connection type '{type}' has no org label."),
    };

    /// <summary>
    /// The canonical wire/YAML name of an enum value: its EnumMember value when
    /// declared (e.g. <c>azure_devops</c>), else the lower-cased member name.
    /// </summary>
    public static string WireName<TEnum>(TEnum value) where TEnum : struct, Enum =>
        typeof(TEnum).GetMember(value.ToString())[0]
            .GetCustomAttribute<EnumMemberAttribute>()?.Value
        ?? value.ToString().ToLowerInvariant();

    private static string? FieldValue(TrackerEntity tracker, string key) => key switch
    {
        "url" => tracker.Url,
        "organization" => tracker.Organization,
        "project" => tracker.Project,
        "authSecret" => tracker.AuthSecret,
        "email" => tracker.Email,
        "doneStatus" => tracker.DoneStatus,
        "failedStatus" => tracker.FailedStatus,
        "needsClarificationStatus" => tracker.NeedsClarificationStatus,
        "notImplementableStatus" => tracker.NotImplementableStatus,
        "closeTransitionName" => tracker.CloseTransitionName,
        "defaultPipeline" => tracker.DefaultPipeline,
        _ => null,
    };

    /// <summary>
    /// Rejects an agent whose roles are not the fixed set, whose primary names no entry, whose
    /// role names an entry the catalog does not declare, or whose catalog entry names no model
    /// or a model priced by neither the agent's pricing table (exact id) nor the bundled price
    /// list (exact id or bare name). No prefix match: a typo that starts with a real id must not
    /// pass as that id.
    /// </summary>
    public static void ValidateAgent(AgentEntity agent, IBundledModelPriceList priceList)
    {
        ValidateRoles(agent);
        var overrides = (agent.Pricing?.Models.Keys ?? Enumerable.Empty<string>())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, entry) in agent.Catalog)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(entry.Model))
                throw new ConfigurationException($"Agent '{agent.Id}': catalog entry '{name}' names no model.");
            if (!overrides.Contains(entry.Model) && priceList.Find(entry.Model) is null)
                throw new ConfigurationException(
                    $"Agent '{agent.Id}': catalog entry '{name}' uses model '{entry.Model}' " +
                    "which has no pricing entry — the bundled price list does not know it; " +
                    "add it to the agent's pricing table.");
        }
    }

    private static void ValidateRoles(AgentEntity agent)
    {
        foreach (var (role, use) in agent.Models)
        {
            if (!RoleKeys.Contains(role, StringComparer.Ordinal))
                throw new ConfigurationException(
                    $"Agent '{agent.Id}': unknown model role '{role}' (known: {string.Join(", ", RoleKeys)}).");
            if (!string.IsNullOrWhiteSpace(use) && !agent.Catalog.ContainsKey(use))
                throw new ConfigurationException(
                    $"Agent '{agent.Id}': role '{role}' uses catalog entry '{use}', which the catalog does not " +
                    $"declare (declared: {string.Join(", ", agent.Catalog.Keys)}).");
        }
        if (!agent.Models.TryGetValue("primary", out var primary) || string.IsNullOrWhiteSpace(primary))
            throw new ConfigurationException(
                $"Agent '{agent.Id}': the primary role must use a catalog entry.");
    }
}
