using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>
/// 2026-11-23-c81ed: a delete's plan — every file under the deploy folder at any depth (c81ea's recursive listing, so a
/// subfolder an operator added goes too, or it would keep the Application alive) as a deletion. Nothing found plans
/// nothing; a truncated listing is refused with c81eb's sentence naming pocs.gitops.max_folder_files.
/// </summary>
public sealed class PocAbsentPlan
{
    public async Task<(PocFolderChanges? Plan, string? Refusal)> PlanAsync(
        ISourceProvider provider, BranchName branch, string folder, int maxFiles, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var listing = await provider.ListFilesUnderAsync(folder, branch, maxFiles, ct);
        if (listing.Truncated)
            return (null, $"the deploy folder {folder}/ holds more than {maxFiles} files (pocs.gitops.max_folder_files); it holds "
                + "a handful of rendered manifests, so files someone else put there are not planned for deletion");
        return (new PocFolderChanges([.. listing.Files.Order(StringComparer.Ordinal).Select(RepoFile.Deletion)], []), null);
    }
}
