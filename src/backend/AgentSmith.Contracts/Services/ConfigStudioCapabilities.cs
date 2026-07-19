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
            .Select(t => new TrackerTypeCapability(WireName(t), TrackerFields(t))).ToList(),
        ConnectionTypes: Enum.GetValues<RepoType>()
            .Where(t => t != RepoType.Local) // Local is a repo locator, not a discoverable git host.
            .Select(t => new ConnectionTypeCapability(WireName(t), OrgLabel(t), ConnectionFields(t))).ToList(),
        AgentProviders: agentProviders.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.Ordinal).ToList(),
        ResolutionStrategies: ResolutionStrategyNames,
        Pipelines: PipelinePresets.Names,
        Roles: RoleCapabilities);

    /// <summary>The reserved model role carrying the agent's top-level model/deployment.</summary>
    public const string ReservedCodingRole = "coding";

    /// <summary>
    /// The fixed model-role set the agent form renders — the reserved 'coding'
    /// (required) plus every <see cref="TaskType"/> role (reasoning optional).
    /// Keys are camelCased TaskType names, matching ConfigCatalogMapper's models map.
    /// </summary>
    public static IReadOnlyList<ModelRoleCapability> RoleCapabilities { get; } =
        new[] { new ModelRoleCapability(ReservedCodingRole, Optional: false) }
            .Concat(Enum.GetValues<TaskType>()
                .Select(t => new ModelRoleCapability(RoleKey(t), Optional: t == TaskType.Reasoning)))
            .ToList();

    /// <summary>The valid model-role keys (coding + the TaskType roles).</summary>
    public static IReadOnlyList<string> RoleKeys { get; } =
        RoleCapabilities.Select(r => r.Key).ToList();

    private static readonly HashSet<string> ValidRoleKeys = RoleKeys.ToHashSet(StringComparer.Ordinal);

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
        var type = Enum.GetValues<TrackerType>()
            .Where(t => WireName(t).Equals(tracker.Type, StringComparison.OrdinalIgnoreCase))
            .Cast<TrackerType?>().FirstOrDefault()
            ?? throw new ConfigurationException(
                $"Tracker '{tracker.Id}': unknown type '{tracker.Type}' " +
                $"(known: {string.Join(", ", TrackerTypeNames)}).");

        var missing = TrackerFields(type)
            .Where(f => f.Required && string.IsNullOrWhiteSpace(FieldValue(tracker, f.Key)))
            .Select(f => f.Key)
            .ToList();
        if (missing.Count > 0)
            throw new ConfigurationException(
                $"Tracker '{tracker.Id}' (type {tracker.Type}): missing required field(s) " +
                $"{string.Join(", ", missing)}.");
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

    private static IReadOnlyList<CapabilityField> TrackerFields(TrackerType type) => type switch
    {
        // Grounded in TicketProviderFactory: ADO builds its org URL from
        // organization + project; GitHub/Jira connect by URL; GitLab addresses
        // the project path. Every tracker authenticates via a secret NAME.
        TrackerType.AzureDevOps =>
        [
            new CapabilityField("organization", "Organization", Required: true),
            new CapabilityField("project", "Project", Required: true),
            new CapabilityField("url", "URL", Required: false),
            new CapabilityField("authSecret", "Auth secret", Required: true),
        ],
        TrackerType.GitHub =>
        [
            new CapabilityField("url", "Repository URL", Required: true),
            new CapabilityField("authSecret", "Auth secret", Required: true),
        ],
        TrackerType.GitLab =>
        [
            new CapabilityField("project", "Project path", Required: true),
            new CapabilityField("url", "Base URL", Required: false),
            new CapabilityField("authSecret", "Auth secret", Required: true),
        ],
        TrackerType.Jira =>
        [
            new CapabilityField("url", "Base URL", Required: true),
            new CapabilityField("project", "Project key", Required: false),
            new CapabilityField("authSecret", "Auth secret", Required: true),
        ],
        _ => throw new ConfigurationException(
            $"Tracker type '{type}' has no capabilities descriptor — add its field set."),
    };

    private static IReadOnlyList<CapabilityField> ConnectionFields(RepoType type) => type switch
    {
        // Grounded in RawConnectionEntry + the connection patcher: ADO needs
        // organization + team project, GitHub an owner, GitLab a group.
        RepoType.AzureDevOps =>
        [
            new CapabilityField("organization", "Organization", Required: true),
            new CapabilityField("project", "Project", Required: true),
            new CapabilityField("authSecret", "Auth secret", Required: true),
            new CapabilityField("defaultBranch", "Default branch", Required: false),
        ],
        RepoType.GitHub or RepoType.GitLab =>
        [
            new CapabilityField("organization", type == RepoType.GitHub ? "Owner" : "Group", Required: true),
            new CapabilityField("authSecret", "Auth secret", Required: true),
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
        _ => null,
    };

    /// <summary>
    /// Rejects an agent that names a model role outside the fixed set, or whose
    /// role model has no pricing entry on the agent — the studio's roles are the
    /// TaskType set, and every routed model must be priced.
    /// </summary>
    public static void ValidateAgent(AgentEntity agent)
    {
        foreach (var key in agent.Models.Keys)
            if (!ValidRoleKeys.Contains(key))
                throw new ConfigurationException(
                    $"Agent '{agent.Id}': unknown model role '{key}' " +
                    $"(known: {string.Join(", ", RoleKeys)}).");

        var priced = (agent.Pricing?.Models.Keys ?? Enumerable.Empty<string>())
            .ToHashSet(StringComparer.Ordinal);
        foreach (var (role, assignment) in agent.Models)
        {
            if (string.IsNullOrWhiteSpace(assignment.Model)) continue; // an unset optional role
            if (!priced.Contains(assignment.Model))
                throw new ConfigurationException(
                    $"Agent '{agent.Id}': role '{role}' uses model '{assignment.Model}' " +
                    "which has no pricing entry — add it to the agent's pricing table.");
        }
    }

    /// <summary>The camelCased wire key for a model role, matching ConfigCatalogMapper's models map.</summary>
    private static string RoleKey(TaskType role)
    {
        var name = role.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
