using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Moq;

namespace AgentSmith.Tests.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: a default branch held in memory behind ISourceProvider — listing, reads and checkout-free
/// writes with deletions; <see cref="ConflictsLeft"/> answers that many writes as Conflict after <see cref="OnConflict"/>
/// moved the head; <see cref="FailWrites"/> fails every write.</summary>
internal sealed class FakeGitOpsBranch
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);

    public List<IReadOnlyList<RepoFile>> Writes { get; } = [];

    public int ConflictsLeft { get; set; }

    public Action? OnConflict { get; set; }

    public bool FailWrites { get; set; }

    public bool TruncateListings { get; set; }

    public ISourceProviderFactory Factory()
    {
        var provider = new Mock<ISourceProvider>();
        provider.Setup(p => p.GetDefaultBranchAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new BranchName("main"));
        provider.Setup(p => p.TryReadFileAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, BranchName _, CancellationToken _) => Files.GetValueOrDefault(path));
        provider.Setup(p => p.ListFilesUnderAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string folder, BranchName _, int max, CancellationToken _) => RepoFileListing.Bounded(
                Files.Keys.Where(k => k.StartsWith(folder + "/", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToList(), max, TruncateListings));
        provider.Setup(p => p.WriteFilesToBranchAsync(It.IsAny<BranchName>(), It.IsAny<IReadOnlyList<RepoFile>>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<BranchName?>()))
            .ReturnsAsync((BranchName _, IReadOnlyList<RepoFile> files, string _, CancellationToken _, BranchName? _) => Write(files));
        return Mock.Of<ISourceProviderFactory>(f => f.Create(It.IsAny<AgentSmith.Contracts.Models.Configuration.RepoConnection>()) == provider.Object);
    }

    private BranchWriteResult Write(IReadOnlyList<RepoFile> files)
    {
        if (FailWrites) return BranchWriteResult.Failed("the host refused the push");
        if (ConflictsLeft > 0)
        {
            ConflictsLeft--;
            OnConflict?.Invoke();
            return BranchWriteResult.Conflicted("the head moved");
        }
        Writes.Add(files);
        foreach (var file in files)
            if (file.Delete) Files.Remove(file.Path);
            else Files[file.Path] = file.Content;
        return BranchWriteResult.Ok($"sha{Writes.Count}");
    }
}
