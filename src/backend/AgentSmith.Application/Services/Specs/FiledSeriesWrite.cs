using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7d: filing's checkout-free write of a placed series — each member's sub-series onto
/// its own ticket branch through its source provider, all from one set, so every member carries the
/// same revision time and the same member list. The first member that does not answer a commit ends
/// the write with its name. A ratified mock goes to the member whose sub-series holds its spec.
/// </summary>
public sealed class FiledSeriesWrite(ISourceProviderFactory sources, SeriesFiles files)
{
    public async Task<SpecSetWriteResult> WriteAsync(SeriesPlacement placement, BranchName branch,
        CancellationToken cancellationToken, IReadOnlyDictionary<string, RepoFile>? mocks = null)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var shas = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var repo in placement.Members)
        {
            var sub = placement.Set.OfRepository(repo.Name);
            var result = await sources.Create(repo).WriteFilesToBranchAsync(branch,
                [.. files.Render(sub).Select(f => new RepoFile(f.Path, f.Content)),
                    .. sub.UnexecutedTail.Select(s => mocks?.GetValueOrDefault(s.SpecId)).OfType<RepoFile>()],
                SeriesWriter.MessageFor(sub), cancellationToken);
            if (!result.Written)
            {
                var why = result.Error ?? "the write named no commit";
                return SpecSetWriteResult.Failed(placement.Members.Count > 1 ? $"{repo.Name}: {why}" : why);
            }
            shas[repo.Name] = result.CommitSha!;
        }
        var carrier = placement.Carrier?.Name;
        return SpecSetWriteResult.Ok(shas, carrier is not null && shas.TryGetValue(carrier, out var own)
            ? own : shas.Values.FirstOrDefault() ?? string.Empty);
    }
}
