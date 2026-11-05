using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-04-6768a: whether a false premise is false only because the series' own commits flipped what
/// its cited look finds. A yes/no question git answers, with no model and no text match on the premise.
/// <para>
/// The look is replayed on the repository's PRIMARY sandbox, the one checkpoint commits are made in
/// (<see cref="RepoWorkPusher"/>). It is superseded only when a path flipped between the series base and
/// HEAD AND a series checkpoint that is no merge and is not reachable from the base ref touched it, so a
/// change merged in from the base is someone else's and the premise still goes back. Every other outcome
/// — a look of another kind, an unreadable base, a failed git step — leaves the finding false.
/// </para>
/// </summary>
public sealed class PremiseBaseReplay(
    SandboxTargets targets, SeriesBase series, PremiseLookFlip flip, StructureGitRunner git,
    ILogger<PremiseBaseReplay> logger)
{
    public async Task<PremiseSupersession?> SupersededAsync(
        PipelineContext pipeline, SpecSet set, DerivationLook look, PremiseFinding finding,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(finding);
        var cited = look.Evidence.Looks.FirstOrDefault(l => l.Id == finding.CitedId);
        if (cited is null || !PremiseLookFlip.Replays(cited)) return Stays(finding, "its cited look is no search or read");
        if (Primary(pipeline, cited.Repository) is not { } sandbox)
            return Stays(finding, $"the run has no sandbox for {cited.Repository}");
        var start = await series.ResolveAsync(sandbox, set, RunParentTicket.Of(pipeline), cancellationToken);
        if (start is null || start.IsHead) return Stays(finding, "no series base with series work after it");
        var paths = await flip.FlippedAsync(sandbox, cited, start.Commit, cancellationToken);
        if (paths is null) return Stays(finding, "a replay step failed");
        if (paths.Count == 0) return Stays(finding, $"nothing flipped between {start.Commit} and HEAD");
        var commits = await SeriesCommitsAsync(sandbox, start, paths, cancellationToken);
        if (commits is not { Count: > 0 }) return Stays(finding, "no series checkpoint touched the flipped paths");
        return new PremiseSupersession(finding, start.Commit, paths, commits);
    }

    private async Task<IReadOnlyList<string>?> SeriesCommitsAsync(
        ISandbox sandbox, SeriesStart start, IReadOnlyList<string> paths, CancellationToken ct)
    {
        var log = await git.RunAsync(sandbox,
            ["log", "--no-merges", "--fixed-strings", $"--grep={RunCheckpointCommit.Marker}", "--format=%H",
                $"{start.Commit}..HEAD", "--not", start.BaseRef, "--", .. paths], ct);
        return log.ExitCode != 0 ? null
            : (log.OutputContent ?? string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>The first sandbox of the repository that owns the look's key — checkpoint commits land
    /// there and nowhere else; the look's own sandbox when the run lists no repositories.</summary>
    private ISandbox? Primary(PipelineContext pipeline, string key)
    {
        if (pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) && repos is not null)
            foreach (var repo in repos)
                if (targets.SandboxesForRepo(pipeline, repo) is { Count: > 0 } matches && matches.Any(m => m.Key == key))
                    return matches[0].Value;
        return targets.TryResolve(pipeline, out var sandboxes, out _) ? sandboxes.GetValueOrDefault(key) : null;
    }

    private PremiseSupersession? Stays(PremiseFinding finding, string why)
    {
        logger.LogInformation("The premise \"{Premise}\" stays false: {Why}", finding.Premise, why);
        return null;
    }
}
