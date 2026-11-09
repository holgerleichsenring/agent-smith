using System.Collections.Concurrent;
using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-09-0620b: a repository's top two levels, read through its source provider on the default branch — no
/// sandbox, no clone. A design turn on a repository nobody has mapped yet otherwise sees only its name, and asks the
/// operator what the repository could have told it. A listing is kept a few minutes, so a conversation's turns do not
/// list the same tree again; a repository that cannot be listed yields nothing and the turn reads through its tools.
/// </summary>
public sealed class RepositoryLayoutReader(
    ISourceProviderFactory providers,
    TimeProvider clock,
    ILogger<RepositoryLayoutReader> logger)
{
    private const int RootEntries = 40;
    private const int ChildEntries = 20;
    private static readonly TimeSpan Kept = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, (DateTimeOffset At, string Layout)> _kept = new();

    public async Task<string?> ReadAsync(RepoConnection repo, CancellationToken ct)
    {
        if (!repo.HasLocation) return null;
        var now = clock.GetUtcNow();
        if (_kept.TryGetValue(repo.Name, out var kept) && now - kept.At < Kept) return kept.Layout;
        try
        {
            var provider = providers.Create(repo);
            var root = await provider.ListDirectoryAsync(string.Empty, ct);
            if (root.Count == 0) return null;
            var shown = root.Order(StringComparer.Ordinal).Take(RootEntries).ToList();
            var children = await Task.WhenAll(shown.Select(name => ChildrenAsync(provider, name, ct)));
            var layout = Render(shown, children, root.Count);
            _kept[repo.Name] = (now, layout);
            return layout;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogInformation(ex, "Could not list the layout of {Repo}; the turn reads it through its tools.", repo.Name);
            return null;
        }
    }

    // A file lists as nothing, as itself, or as an error, depending on the provider; only a listing of other names is a
    // directory.
    private static async Task<IReadOnlyList<string>?> ChildrenAsync(ISourceProvider provider, string name, CancellationToken ct)
    {
        try
        {
            var listed = await provider.ListDirectoryAsync(name, ct);
            return listed.Count == 0 || (listed.Count == 1 && listed[0] == name) ? null : listed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static string Render(List<string> shown, IReadOnlyList<string>?[] children, int total)
    {
        var sb = new StringBuilder("No code map is cached for this repository yet. Its top two levels on the default "
            + "branch, directories ending in /:\n");
        for (var i = 0; i < shown.Count; i++)
        {
            if (children[i] is not { } inner) { sb.Append("- ").AppendLine(shown[i]); continue; }
            sb.Append("- ").Append(shown[i]).AppendLine("/");
            foreach (var child in inner.Order(StringComparer.Ordinal).Take(ChildEntries))
                sb.Append("  - ").AppendLine(child);
            if (inner.Count > ChildEntries) sb.Append("  - … ").Append(inner.Count - ChildEntries).AppendLine(" more");
        }
        if (total > shown.Count) sb.Append("- … ").Append(total - shown.Count).AppendLine(" more");
        return sb.ToString().TrimEnd();
    }
}
