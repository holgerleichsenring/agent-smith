using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565c: the tracked files under a workdir with one of the given extensions,
/// repository-relative, as git lists them — build output and generated directories are not in
/// git, so no exclusion list is kept here. A listing the capture may have cut is refused: a
/// partial baseline would tolerate nothing it missed. 2026-10-13-2d0c: a -z listing is one line,
/// which the capture never cuts, so it is accepted at any length.
/// </summary>
public sealed class StructureSourceListing(StructureGitRunner git, StructureZeroListing listing)
{
    public async Task<(IReadOnlyList<string> Paths, string? Refusal)> ListAsync(
        ISandbox sandbox, string workdir, IReadOnlyList<string> extensions,
        CancellationToken cancellationToken)
    {
        var result = await git.RunAsync(
            sandbox, ["ls-files", "-z", "--full-name", "--", .. Pathspecs(workdir, extensions)], cancellationToken);
        if (result.ExitCode != 0) return ([], "the source is not a git checkout");
        return listing.Parse(result.OutputContent ?? string.Empty) is { } paths
            ? (paths, null)
            : ([], "the file listing reached the capture cap and may be incomplete");
    }

    // A pathspec's '*' crosses directories, so "src/Api/*.cs" names every C# file below src/Api.
    private static IEnumerable<string> Pathspecs(string workdir, IReadOnlyList<string> extensions) =>
        extensions.Select(e => workdir.Length == 0 ? $"*{e}" : $"{workdir}/*{e}");
}
