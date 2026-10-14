using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762n: re-grounds the ENTERED spec's path:line citations, mechanically. Each citation is
/// compared with the tree it was written against: unchanged lines hold; lines whose exact text is found once
/// elsewhere move — in their own file, or into the one other file holding them (a build note's move tried
/// first); lines found nowhere are a STALE citation, told to the master and never rewritten. A token a
/// ':'-continuation binds to stays in its file, so the continuation never silently re-points.
/// </summary>
public sealed class SpecCitationRegrounder(
    SpecCitationHistory history, EvidenceReferences references, ILogger<SpecCitationRegrounder> logger)
{
    public async Task<SpecReground> RegroundAsync(PipelineContext pipeline, SpecSet set, DerivedSpec spec, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(spec);
        var tokens = SpecCitationTokens.Of(spec.Draft.Yaml, references);
        if (tokens.Count == 0 || SpecReviewRepositories.OnePerRepository(pipeline) is not { Count: > 0 } repositories)
            return SpecReground.Unchanged(spec.Draft.Yaml);
        var session = new SpecRegroundSession(
            repositories, PrecedingSpecs.NotesOf(pipeline, spec.SpecId), spec.SpecId, set.Approval?.At);
        foreach (var token in tokens) await GroundAsync(session, token, ct);
        if (session.Unchecked > 0)
            logger.LogInformation("{SpecId}: {Count} citation(s) past the budget of {Max} file(s) were not checked",
                spec.SpecId, session.Unchecked, SpecRegroundSession.MaxPairs);
        return Splice(spec.Draft.Yaml, session);
    }

    private async Task GroundAsync(SpecRegroundSession session, CitedToken token, CancellationToken ct)
    {
        if (await RouteAsync(session, token, ct) is not { } route || !session.Admit(route.Repository, route.Path)) return;
        if (await history.UnchangedAsync(route.Sandbox, route.Base, route.Path, ct)) return;
        if (await history.OldTextAsync(route.Sandbox, route.Base, route.Path, ct) is not { } old) return;
        var lines = SpecCitationLocator.Lines(old);
        var blocks = token.Ranges.Select(r => SpecCitationLocator.Block(lines, r)).ToList();
        if (blocks.Any(b => b is null)) return;
        var place = await PlaceAsync(session, route, [.. blocks.Select(b => b!)], token, ct);
        if (place is null) return;
        if (place is not { Path: { } path } found)
        {
            session.Stale.Add($"{token.Text} ({route.Repository}): its lines changed since {Short(route.Base)} and their text was found nowhere");
            return;
        }
        if (path == route.Path && found.Ranges.SequenceEqual(token.Ranges)) return;
        var moved = route.Prefix + path + ":" + SpecCitationTokens.Format(found.Ranges);
        if (token.Continued && path != route.Path)
            session.Stale.Add($"{token.Text}: its text is now at {moved}, but a ':' citation continues it, so it was not re-pointed");
        else session.Moves.Add(new SpecCitationMove(token.Text, moved, token.Facts));
    }

    // Null: not checked. A null Path: found nowhere. Else where the blocks stand now.
    private async Task<(string? Path, IReadOnlyList<EvidenceLineRange> Ranges)?> PlaceAsync(
        SpecRegroundSession session, CitationRoute route, IReadOnlyList<IReadOnlyList<string>> blocks, CitedToken token,
        CancellationToken ct)
    {
        if (await history.CurrentTextAsync(route.Sandbox, route.Path, ct) is { } current)
        {
            var inFile = SpecCitationLocator.InFile(blocks, token.Ranges, current);
            if (inFile is null || inFile.Count > 0) return (route.Path, inFile ?? token.Ranges);
        }
        var line = SpecCitationLocator.SearchLine(blocks);
        var grepped = line is null ? [] : await history.CandidatesAsync(route.Sandbox, line, ct) ?? [];
        var candidates = SpecCitationLocator.Candidates(
            SpecCitationLocator.NoteDestinations(session.Notes, route.Path), grepped, route.Path);
        if (candidates is null) return (null, token.Ranges);
        if (!session.Spend(candidates.Count)) return null;
        var texts = new List<(string Path, string Text)>();
        foreach (var candidate in candidates)
            if (await history.CurrentTextAsync(route.Sandbox, candidate, ct) is { } text) texts.Add((candidate, text));
        return SpecCitationLocator.InOne(blocks, texts) is { } hit ? hit : (null, token.Ranges);
    }

    // As the filesystem tools route it: the longest repository name the path starts with (stripped), the only
    // repository, else — a bare path in a multi-repository run — the one repository holding it at its base.
    private async Task<CitationRoute?> RouteAsync(SpecRegroundSession session, CitedToken token, CancellationToken ct)
    {
        if (await history.WriteAsync(session.Repositories, session.SpecId, token.Text, ct) is not { } write) return null;
        var approvedAt = write.IsAdded ? session.ApprovedAt : null;
        var path = token.Reference.Path;
        var (key, matched) = RepoPathRouter.MatchLongestKey(session.Repositories, path);
        IEnumerable<(string Name, Contracts.Sandbox.ISandbox Sandbox, string Path, string Prefix)> tried =
            matched is not null && path.Length > key.Length + 1 ? [(key, matched, path[(key.Length + 1)..], key + "/")]
            : session.Repositories.Select(r => (r.Key, r.Value, path, string.Empty));
        var holding = new List<CitationRoute>();
        foreach (var (name, sandbox, at, prefix) in tried)
            if (await history.BaseAsync(sandbox, name, write, approvedAt, ct) is { } baseSha
                && (session.Repositories.Count == 1 || matched is not null || await history.ExistsAsync(sandbox, baseSha, at, ct)))
                holding.Add(new CitationRoute(name, sandbox, at, prefix, baseSha));
        return holding.Count == 1 ? holding[0] : null;
    }

    private static SpecReground Splice(string yaml, SpecRegroundSession session)
    {
        var applied = new List<SpecCitationMove>();
        foreach (var move in session.Moves)
        {
            if (SpecFactRegrounding.ApplyAll(yaml, move.Facts, move.Old, move.New) is not { } next)
            {
                session.Stale.Add($"{move.Old}: its text is now at {move.New}, but the evidence could not be rewritten verbatim");
                continue;
            }
            yaml = next;
            applied.Add(move);
        }
        return new SpecReground(yaml, applied, session.Stale, session.Unchecked);
    }

    private static string Short(string sha) => sha.Length <= 8 ? sha : sha[..8];
}
