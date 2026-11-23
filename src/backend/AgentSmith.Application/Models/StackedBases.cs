namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-11-23-31dca: the unlanded prerequisite whose branch each repo of a stacked run is cut from, as the claim stamped it.
/// A repo without an entry has no prerequisite with work in it and cuts from its own base; a parked run's old single-id
/// stamp names <see cref="EveryRepo"/>, the one id it meant for every repo.
/// </summary>
public sealed record StackedBases(IReadOnlyDictionary<string, string> PerRepo, string? EveryRepo = null)
{
    /// <summary>Whether the claim named this repo itself — the old single-id stamp named no repo, so its missing branch in a repo
    /// that prerequisite never touched is a fall-through, as it was when it was stamped.</summary>
    public bool Names(string? repo) => repo is not null && PerRepo.ContainsKey(repo);

    /// <summary>The prerequisite this repo is cut from; null when it cuts from its own base.</summary>
    public string? For(string? repo) =>
        repo is not null && PerRepo.TryGetValue(repo, out var id) ? id : EveryRepo;
}
