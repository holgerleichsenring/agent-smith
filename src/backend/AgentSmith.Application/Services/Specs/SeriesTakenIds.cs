using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7d: what a new spec id must never be — a spec file in the done/ directory of ANY
/// run repository (a series member or not, so a repository the series left still counts) and every
/// executed id the pointer stored. Re-minting either would give two specs one id.
/// </summary>
public sealed class SeriesTakenIds(ISandboxFileReaderFactory readerFactory, SandboxTargets sandboxTargets)
{
    /// <param name="stored">The set the pointer's count is back-filled against: the branch's, else the approval's.</param>
    public async Task<IReadOnlyList<string>> ReadAsync(
        PipelineContext pipeline, IReadOnlyList<RepoConnection> repos, SpecSetPointer? pointer,
        SpecSet? stored, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repos);
        var taken = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var repo in repos)
        {
            var matches = sandboxTargets.SandboxesForRepo(pipeline, repo);
            if (matches.Count == 0) continue;
            var listed = await readerFactory.Create(matches[0].Value).ListAsync(SeriesPaths.Done, maxDepth: 1, cancellationToken);
            taken.UnionWith(listed.Select(SeriesPaths.FileName).Where(SeriesPaths.IsSpecFile));
        }
        taken.UnionWith(pointer?.ExecutedIdsOf(stored?.Specs ?? []) ?? []);
        return [.. taken];
    }
}
