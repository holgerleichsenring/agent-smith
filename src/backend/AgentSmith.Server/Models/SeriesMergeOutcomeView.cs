namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dce: what a merge from the series page did to one repo's pull request.</summary>
/// <param name="Outcome">merged · armed (the host merges once its policy passes) · refused.</param>
/// <param name="Text">The run's sentence for it, naming the repo and the host's or guard's reason.</param>
public sealed record SeriesMergeOutcomeView(string Repo, string Outcome, string Text)
{
    /// <summary>2026-11-25-96aeb: the refusal is one an override reason passes.</summary>
    public bool Overridable { get; init; }

    /// <summary>2026-11-25-96aeb: the refusals the merge went past.</summary>
    public IReadOnlyList<string> Overrode { get; init; } = [];
}
