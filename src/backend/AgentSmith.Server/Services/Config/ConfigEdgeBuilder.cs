namespace AgentSmith.Server.Services.Config;

/// <summary>
/// p0266: expands the redacted projects into the config graph's reachability
/// edges — one edge per (project → linked entity), tagged by kind so the
/// dashboard can colour and route them. Derived purely from the already-mapped
/// <see cref="ConfigProject"/> list; needs no access to the raw config.
/// </summary>
public static class ConfigEdgeBuilder
{
    public static IReadOnlyList<ConfigEdge> Build(IReadOnlyList<ConfigProject> projects)
    {
        var edges = new List<ConfigEdge>();
        foreach (var project in projects)
        {
            edges.Add(new ConfigEdge(project.Name, project.AgentName, "agent"));
            edges.Add(new ConfigEdge(project.Name, project.TrackerName, "tracker"));
            edges.AddRange(project.RepoNames.Select(r => new ConfigEdge(project.Name, r, "repo")));
            edges.AddRange(project.Pipelines.Select(p => new ConfigEdge(project.Name, p, "pipeline")));
        }
        return edges;
    }
}
