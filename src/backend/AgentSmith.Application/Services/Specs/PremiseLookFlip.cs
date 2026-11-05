using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-04-6768a: replays ONE cited look with git at two refs and names the paths whose presence
/// flipped — matched at one ref and not at the other for a search, the file there at exactly one ref for
/// a read. A content change in a path present at both is no flip: it does not show that the premise held.
/// <para>
/// The replay never compares with what the original tool printed: that ran over the working tree, this
/// runs over commits, so only the two git answers are compared with each other.
/// </para>
/// </summary>
public sealed class PremiseLookFlip(StructureGitRunner git)
{
    /// <summary>The flipped paths, empty when nothing flipped, or null when a git step failed or the look
    /// is of a kind with no replay.</summary>
    public async Task<IReadOnlyList<string>?> FlippedAsync(
        ISandbox sandbox, EvidenceLook look, string baseCommit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(look);
        var then = await PresentAsync(sandbox, look, baseCommit, cancellationToken);
        if (then is null) return null;
        var now = await PresentAsync(sandbox, look, SeriesStart.Head, cancellationToken);
        if (now is null) return null;
        return [.. then.Except(now).Concat(now.Except(then)).Order(StringComparer.Ordinal)];
    }

    /// <summary>Whether the look's kind can be replayed at all.</summary>
    public static bool Replays(EvidenceLook look)
    {
        ArgumentNullException.ThrowIfNull(look);
        return look.Kind switch
        {
            EvidenceRecord.Search => !string.IsNullOrWhiteSpace(look.Pattern),
            EvidenceRecord.Read => !string.IsNullOrWhiteSpace(look.Path),
            _ => false,
        };
    }

    private Task<HashSet<string>?> PresentAsync(ISandbox sandbox, EvidenceLook look, string at, CancellationToken ct) =>
        look.Kind switch
        {
            EvidenceRecord.Search when Replays(look) => MatchedAsync(sandbox, look, at, ct),
            EvidenceRecord.Read when Replays(look) => ListedAsync(sandbox, look.Path!, at, ct),
            _ => Task.FromResult<HashSet<string>?>(null),
        };

    // Exit 1 is grep's "no match", an answer like any other; anything above it is a failure.
    private async Task<HashSet<string>?> MatchedAsync(ISandbox sandbox, EvidenceLook look, string at, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(SearchCommands.OverRef(at, look.Pattern!, look.Path), progress: null, ct);
        if (result.ExitCode is not (0 or 1)) return null;
        var prefix = at + ":";
        return [.. Lines(result.OutputContent)
            .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
            .Select(line => line[prefix.Length..])
            .Select(rest => rest.Split(':', 2)[0])];
    }

    // ls-tree exits 0 with nothing listed for a missing path and 128 for an unreadable ref, so the two
    // are told apart in one step.
    private async Task<HashSet<string>?> ListedAsync(ISandbox sandbox, string path, string at, CancellationToken ct)
    {
        var result = await git.RunAsync(sandbox, ["ls-tree", "--name-only", at, "--", path], ct);
        if (result.ExitCode != 0) return null;
        return Lines(result.OutputContent).Count == 0 ? [] : [path];
    }

    private static List<string> Lines(string? output) =>
        [.. (output ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries)];
}
