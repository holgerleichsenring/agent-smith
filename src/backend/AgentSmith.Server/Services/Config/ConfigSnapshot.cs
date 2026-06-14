namespace AgentSmith.Server.Services.Config;

/// <summary>
/// p0266: the dashboard's READ contract for the resolved agent-smith config —
/// "how the system is wired", the config-time complement to the per-run
/// topology. This is an explicit ALLOW-LIST: it carries only safe, display-only
/// fields. No api keys, tracker tokens, repo auth references, secret map, or DB
/// connection string is ever mapped onto it, so the endpoint cannot leak a
/// secret it never projects (safety in the type system, not a filter step).
/// A field added to <c>AgentSmithConfig</c> later is invisible here until a
/// reviewer deliberately maps it.
/// </summary>
public sealed record ConfigSnapshot(
    IReadOnlyList<ConfigAgent> Agents,
    IReadOnlyList<ConfigRepo> Repos,
    IReadOnlyList<ConfigTracker> Trackers,
    IReadOnlyList<ConfigProject> Projects,
    IReadOnlyList<ConfigEdge> Edges,
    ConfigGlobals Globals);

/// <summary>An AI agent provider, redacted to its non-secret tuning fields.</summary>
public sealed record ConfigAgent(
    string Name,
    string Type,
    string Model,
    int NetworkTimeoutSeconds,
    int MaxFixIterations,
    int? RequestsPerMinute,
    int? InputTokensPerMinute,
    int MaxConcurrentSkillRounds);

/// <summary>A source repo connection. Url is reduced to host only; Auth dropped.</summary>
public sealed record ConfigRepo(
    string Name,
    string Type,
    string? Host,
    string? DefaultBranch);

/// <summary>A ticket tracker connection. Auth dropped; trigger metadata kept.</summary>
public sealed record ConfigTracker(
    string Name,
    string Type,
    string? Project,
    IReadOnlyList<string> OpenStates,
    string? DoneStatus);

/// <summary>A project wired to its agent, tracker, repos and pipelines (names only).</summary>
public sealed record ConfigProject(
    string Name,
    string Pipeline,
    string AgentName,
    string TrackerName,
    IReadOnlyList<string> RepoNames,
    IReadOnlyList<string> Pipelines);

/// <summary>
/// A reachability edge for the config graph. <see cref="Kind"/> is one of
/// <c>repo</c> | <c>tracker</c> | <c>agent</c> | <c>pipeline</c>; From is the
/// project name, To the linked entity name.
/// </summary>
public sealed record ConfigEdge(string From, string To, string Kind);
