using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-04-6768a: the series base, read off the branch — the state the series' CURRENT text was cut
/// against, before any of its own work. Nothing stores it: the per-spec start heads move at every spec.
/// <para>
/// The anchor is the newest revision commit of this series that is not a citation re-ground, because a
/// re-cut writes facts against the state earlier runs left, while a re-ground only moves citations after
/// a sibling's work. The series' work is then every checkpoint after the anchor that is neither a merge
/// nor reachable from the base ref, and the base is the oldest one's first parent.
/// </para>
/// </summary>
public sealed class SeriesBase(SandboxBaseLadder ladder, StructureGitRunner git, ILogger<SeriesBase> logger)
{
    /// <summary>The series start in this sandbox, or null when the set has no series id, the clone names
    /// no base, no revision of the series is on the branch, or the history cannot be read.</summary>
    public async Task<SeriesStart?> ResolveAsync(
        ISandbox sandbox, SpecSet set, string? parentTicketId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(set);
        if (string.IsNullOrWhiteSpace(set.Series)) return Unread("the set carries no series id");
        if ((await ladder.ResolveAsync(sandbox, parentTicketId, cancellationToken)).Ref is not { } baseRef)
            return Unread("the clone names no base branch");
        if (await AnchorAsync(sandbox, set, cancellationToken) is not { } anchor)
            return Unread($"no revision of series {set.Series} is on the branch");
        var listed = await git.RunAsync(sandbox,
            ["rev-list", "--no-merges", "--fixed-strings", $"--grep={RunCheckpointCommit.Marker}",
                "HEAD", "--not", baseRef, anchor], cancellationToken);
        if (listed.ExitCode != 0) return Unread("the branch history cannot be read");
        var commits = Lines(listed.OutputContent);
        return new SeriesStart(commits.Count == 0 ? SeriesStart.Head : $"{commits[^1]}^", baseRef);
    }

    /// <summary>The subject every revision of this series opens with (<see cref="SeriesWriter.MessageFor"/>).</summary>
    public static string RevisionSubject(SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return $"spec: {set.Series} ({set.Key}) revision";
    }

    private async Task<string?> AnchorAsync(ISandbox sandbox, SpecSet set, CancellationToken ct)
    {
        var subject = RevisionSubject(set);
        var log = await git.RunAsync(sandbox,
            ["log", "--format=%H%x09%s", "--fixed-strings", $"--grep={subject}", "HEAD"], ct);
        if (log.ExitCode != 0) return null;
        return Lines(log.OutputContent)
            .Select(line => line.Split('\t', 2))
            .Where(parts => parts.Length == 2 && parts[1].StartsWith(subject + " ", StringComparison.Ordinal))
            .FirstOrDefault(parts => !parts[1].Contains(
                $"({SpecRevisionCause.CitationsRegrounded}", StringComparison.Ordinal))?[0];
    }

    private SeriesStart? Unread(string why)
    {
        logger.LogInformation("The series base cannot be read: {Why}", why);
        return null;
    }

    private static List<string> Lines(string? output) =>
        [.. (output ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
