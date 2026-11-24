using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: what one GitOps commit changes in a PoC's deploy folder — the files to write and delete —
/// and the sealed files an unchanged seal keeps but the branch lost.</summary>
public sealed record PocFolderChanges(IReadOnlyList<RepoFile> Changes, IReadOnlyList<string> Missing)
{
    public bool IsEmpty => Changes.Count == 0;
}
