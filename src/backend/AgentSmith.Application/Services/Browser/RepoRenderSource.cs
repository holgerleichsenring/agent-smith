using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283dh: copies an HTML file and the directory tree it sits in out of a repository's
/// sandbox into the browser sandbox, so a mock beside a spec or a built page renders from a local
/// origin with the stylesheets and scripts it links. The tree is held to the uploaded-website
/// bounds and refused whole, naming the bound, when it crosses one. Files travel as text — a
/// sandbox read returns UTF-8 only — and a file that is not, or is over one read, is named as not
/// copied rather than silently missing.
/// </summary>
public sealed class RepoRenderSource(ISandboxFileReaderFactory files, RepoTreeListing listing)
{
    internal const string NotCopied = "not copied (binary, or over the 1 MB a sandbox read returns)";
    private const string WorkRoot = "/work";

    public async Task<(StagedSource? Staged, string? Refusal)> CopyAsync(
        ISandbox browser, IReadOnlyDictionary<string, ISandbox> repos, string? repo, string path, CancellationToken ct)
    {
        var (name, sandbox) = await RepositoryOfAsync(repos, repo, path, ct);
        if (sandbox is null) return (null, $"'{path}' is in no repository of this {(repos.Count == 0 ? "scope" : "run or conversation")}");
        var dir = path.Contains('/') ? path[..path.LastIndexOf('/')] : string.Empty;
        var (tree, refusal) = await listing.ListAsync(sandbox, dir, path, ct);
        if (tree is null) return (null, refusal);
        var source = files.Create(sandbox);
        var target = files.Create(browser);
        var root = $"{WorkRoot}/repo/{Guid.NewGuid():N}";
        var notes = new List<string>();
        foreach (var file in tree)
        {
            var text = await source.TryReadAsync(Joined(dir, file), ct);
            if (text is null) notes.Add($"{name}/{Joined(dir, file)}: {NotCopied}");
            else await target.WriteAsync($"{root}/{file}", text, ct);
        }
        return (new StagedSource(null, root, path[(dir.Length == 0 ? 0 : dir.Length + 1)..], notes), null);
    }

    private static string Joined(string dir, string file) => dir.Length == 0 ? file : $"{dir}/{file}";

    // The named repository, or the first one that holds the file when the path named none.
    private async Task<(string Name, ISandbox? Sandbox)> RepositoryOfAsync(
        IReadOnlyDictionary<string, ISandbox> repos, string? repo, string path, CancellationToken ct)
    {
        if (repo is not null) return (repo, repos.GetValueOrDefault(repo));
        foreach (var (name, sandbox) in repos)
            if (await files.Create(sandbox).ExistsAsync(path, ct)) return (name, sandbox);
        return (string.Empty, null);
    }
}
