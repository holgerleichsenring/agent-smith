namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-933da: PoCs — uploaded apps hosted on a target — from agentsmith.yml's top-level <c>pocs:</c> block. Off
/// by default: while <see cref="Enabled"/> is false the PoC routes answer 404 and no PoC hosted service acts. Every
/// provisioned poc- project names <see cref="ProjectAgent"/> and <see cref="ProjectTracker"/>, because a project
/// without either does not resolve.
/// </summary>
public sealed class PocsConfig
{
    public bool Enabled { get; set; }

    /// <summary>The agent every poc- project names.</summary>
    public string? ProjectAgent { get; set; }

    /// <summary>The tracker every poc- project names.</summary>
    public string? ProjectTracker { get; set; }

    /// <summary>Where PoCs are hosted; one for now, the model open for several.</summary>
    public List<PocTargetConfig> Targets { get; set; } = [];

    /// <summary>2026-11-23-933db: what an uploaded PoC archive is held to, and when an import asks whether it is another
    /// app; an absent block reads as the defaults.</summary>
    public PocArchiveLimits Archive { get; set; } = new();

    /// <summary>2026-11-23-933dc: how an upload is scanned before it is committed.</summary>
    public PocScanConfig Scan { get; set; } = new();

    /// <summary>2026-11-23-933dg: how many variables a PoC holds and how large each value may be.</summary>
    public PocVariablesConfig Variables { get; set; } = new();

    public const string BlockedRetentionDaysKey = "pocs.blocked_retention_days";

    /// <summary>2026-11-23-933dc: how many days a blocked upload is kept; 2026-11-23-c81ee purges it after them.</summary>
    public int BlockedRetentionDays { get; set; } = 7;

    /// <summary>What is wrong with these values, one sentence each naming the key, the value and the reason; empty
    /// when nothing is. The ONE rule — the load and the settings store refuse what this names.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>(EnabledProblems());
        foreach (var duplicate in Targets.GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            problems.Add($"pocs.targets[].name '{duplicate.Key}' is used {duplicate.Count()} times; it must be unique, "
                + "because a PoC names its target by it");
        problems.AddRange(Targets.SelectMany(t => t.Problems()));
        problems.AddRange(Archive.Problems());
        problems.AddRange(Scan.Problems());
        problems.AddRange(Variables.Problems());
        if (BlockedRetentionDays < 1)
            problems.Add($"{BlockedRetentionDaysKey} is {BlockedRetentionDays}; it must be 1 or more, because 0 or less would "
                + "purge a blocked upload before anyone could read why it was blocked");
        return problems;
    }

    private IEnumerable<string> EnabledProblems()
    {
        if (!Enabled) yield break;
        if (Targets.Count == 0)
            yield return "pocs.targets is empty while pocs.enabled is true; name one target, because a PoC is hosted on one";
        if (string.IsNullOrWhiteSpace(ProjectAgent))
            yield return "pocs.project_agent is empty while pocs.enabled is true; name an agent, because every poc- project "
                + "needs one to resolve";
        if (string.IsNullOrWhiteSpace(ProjectTracker))
            yield return "pocs.project_tracker is empty while pocs.enabled is true; name a tracker, because every poc- project "
                + "needs one to resolve";
    }
}
