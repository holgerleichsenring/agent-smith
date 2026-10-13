namespace AgentSmith.Server.Services.Init;

/// <summary>2026-10-13-0b82c: whether the name is a configured project, and if so its repositories' bootstrap.</summary>
public sealed record ProjectBootstrapLookup(bool IsKnownProject, IReadOnlyList<ProjectBootstrapEntry> Repos)
{
    public static ProjectBootstrapLookup UnknownProject { get; } = new(false, []);
}
