using AgentSmith.Infrastructure.Models;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-12-6bfaa: one file, one directory listing and branch existence on a NAMED ref, through
/// the same client factory and token the provider uses. The provider's default-branch reads are
/// these with the resolved default as the ref.
/// </summary>
public sealed class GitHubBranchRead(
    GitHubRepoAddress address, IGitHubClientFactory clientFactory, string token)
{
    /// <summary>False only on NotFound; any other failure throws, so it is not read as "absent".</summary>
    public async Task<bool> ExistsAsync(string branch, CancellationToken ct)
    {
        _ = ct;
        try
        {
            await Client().Git.Reference.Get(address.Owner, address.Repo, $"heads/{branch}");
            return true;
        }
        catch (NotFoundException)
        {
            return false;
        }
    }

    // GetAllContentsByRef on a file returns a single-element list carrying the file's text.
    public async Task<string?> TryReadFileAsync(string path, string branch, CancellationToken ct)
    {
        _ = ct;
        try
        {
            var contents = await Client().Repository.Content.GetAllContentsByRef(address.Owner, address.Repo, path, branch);
            return contents.Count == 0 ? null : contents[0].Content;
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    // On a directory the same call returns one entry per child, each with its name.
    public async Task<IReadOnlyList<string>> ListDirectoryAsync(string path, string branch, CancellationToken ct)
    {
        _ = ct;
        try
        {
            var contents = await Client().Repository.Content.GetAllContentsByRef(address.Owner, address.Repo, path, branch);
            return contents.Select(c => c.Name).ToList();
        }
        catch (NotFoundException)
        {
            return [];
        }
    }

    private IGitHubClient Client() => clientFactory.Create(address, token);
}
