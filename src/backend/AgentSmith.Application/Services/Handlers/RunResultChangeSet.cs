using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-13-3890b: what result.md lists as changed, per repository, and the union the
/// progress-ledger coverage reads — every path written <c>&lt;repo&gt;/&lt;path&gt;</c> there,
/// single-repo included, so a target named either way finds its file.
/// </summary>
/// <param name="PerRepository">Keyed by repository name; the run without a repository list
/// is keyed by the empty string.</param>
/// <param name="Notes">One line per sandbox whose branch could not be read.</param>
public sealed record RunResultChangeSet(
    IReadOnlyDictionary<string, IReadOnlyList<CodeChange>> PerRepository,
    IReadOnlyList<CodeChange> Coverage,
    IReadOnlyList<string> Notes)
{
    public const string NoRepository = "";

    public IReadOnlyList<CodeChange> For(string? repoName) =>
        PerRepository.TryGetValue(repoName ?? NoRepository, out var changes) ? changes : [];
}
