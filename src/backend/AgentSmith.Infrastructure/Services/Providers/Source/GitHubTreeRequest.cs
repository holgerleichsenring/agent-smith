using System.Text.Json;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Models;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-23-c81ea: the tree a branch write commits, over the parent's tree so files the write does not name
/// survive it. A write holding no deletion keeps the typed Octokit call it always made. A write holding one posts the
/// whole tree as ONE pre-serialised JSON body: Octokit 14 drops a null <c>sha</c> from a typed tree item, and a null
/// sha is how GitHub's tree API removes a path; a string body reaches the host verbatim.
/// </summary>
public sealed class GitHubTreeRequest(GitHubRepoAddress address)
{
    private const string FileMode = "100644";
    private const string Accepts = "application/vnd.github+json";
    private const string ContentType = "application/json";

    public async Task<TreeResponse> CreateAsync(IGitHubClient client, string parentSha, IReadOnlyList<RepoFile> files)
    {
        if (!files.Any(f => f.Delete))
            return await client.Git.Tree.Create(address.Owner, address.Repo, Typed(parentSha, files));
        var response = await client.Connection.Post<TreeResponse>(
            ApiUrls.Tree(address.Owner, address.Repo), Raw(parentSha, files), Accepts, ContentType);
        return response.Body;
    }

    private static NewTree Typed(string parentSha, IReadOnlyList<RepoFile> files)
    {
        var tree = new NewTree { BaseTree = parentSha };
        foreach (var file in files)
            tree.Tree.Add(new NewTreeItem { Path = file.Path, Mode = FileMode, Type = TreeType.Blob, Content = file.Content });
        return tree;
    }

    // System.Text.Json writes the null, which is the whole point of this body.
    private static string Raw(string parentSha, IReadOnlyList<RepoFile> files) =>
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["base_tree"] = parentSha,
            ["tree"] = files.Select(Item).ToList(),
        });

    private static Dictionary<string, object?> Item(RepoFile file) => file.Delete
        ? new() { ["path"] = file.Path, ["mode"] = FileMode, ["type"] = "blob", ["sha"] = null }
        : new() { ["path"] = file.Path, ["mode"] = FileMode, ["type"] = "blob", ["content"] = file.Content };
}
