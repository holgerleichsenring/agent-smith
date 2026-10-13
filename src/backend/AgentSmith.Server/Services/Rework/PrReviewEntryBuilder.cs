using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-13-9a59b: one pull request's read turned into entries — ONE per host id. A note the state
/// reader names as a decision carries the decision and stands; a decision no note carries (a GitHub
/// approval with an empty body, a threadless Azure DevOps vote) becomes a decision entry. A decision
/// is judged by its reviewer's trust even where the host wrote its note as a system note; any other
/// note is trusted only when a person wrote it.
/// </summary>
public sealed class PrReviewEntryBuilder
{
    public async Task<IReadOnlyList<PrReviewEntry>> BuildAsync(string prKey, RepoType host, PrReviewState state,
        IReadOnlyList<PrReviewThread> threads, PrReviewAuthorTrustOnce trust, CancellationToken ct)
    {
        var decisions = state.Decisions.GroupBy(d => d.HostId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var entries = new Dictionary<string, PrReviewEntry>(StringComparer.Ordinal);
        foreach (var thread in threads)
            foreach (var note in thread.Notes.Where(n => n.HostId.Length > 0 && !entries.ContainsKey(n.HostId)))
                entries[note.HostId] = await NoteAsync(prKey, host, thread, note, decisions.GetValueOrDefault(note.HostId), trust, ct);
        foreach (var decision in decisions.Values.Where(d => !entries.ContainsKey(d.HostId)))
            entries[decision.HostId] = await DecisionAsync(prKey, host, decision, trust, ct);
        return [.. entries.Values];
    }

    private static async Task<PrReviewEntry> NoteAsync(string prKey, RepoType host, PrReviewThread thread, PrReviewNote note,
        PrReviewDecision? decision, PrReviewAuthorTrustOnce trust, CancellationToken ct)
    {
        var author = decision?.Author ?? note.Author;
        var person = decision is not null ? !decision.IsBot : !note.IsBot && !note.IsSystem;
        var entry = New(prKey, note.HostId, thread.FromReview ? PrReviewEntryKinds.Review : PrReviewEntryKinds.Comment,
            author, person && author is not null && await trust.IsTrustedAsync(host, author, ct), note.Body, note.At);
        entry.Decision = decision?.Decision;
        entry.Standing = decision is not null;
        entry.IsOurs = note.IsOurs;
        entry.IsBot = note.IsBot;
        entry.IsSystem = note.IsSystem;
        entry.File = thread.File;
        entry.Line = thread.Line;
        entry.Resolved = thread.Resolved;
        entry.ThreadHostId = thread.HostId.Length > 0 ? thread.HostId : null;
        return entry;
    }

    private static async Task<PrReviewEntry> DecisionAsync(
        string prKey, RepoType host, PrReviewDecision decision, PrReviewAuthorTrustOnce trust, CancellationToken ct)
    {
        var entry = New(prKey, decision.HostId, PrReviewEntryKinds.Decision, decision.Author,
            !decision.IsBot && decision.Author is not null && await trust.IsTrustedAsync(host, decision.Author, ct),
            decision.Body, decision.At);
        entry.Decision = decision.Decision;
        entry.Standing = true;
        entry.IsBot = decision.IsBot;
        return entry;
    }

    private static PrReviewEntry New(
        string prKey, string hostId, string kind, PrCommentAuthor? author, bool trusted, string body, DateTimeOffset at) => new()
    {
        PrKey = prKey, HostId = hostId, Kind = kind, AuthorLogin = author?.AuthorLogin, Trusted = trusted,
        Body = body.Length > PersistenceLimits.ReviewNoteBody ? body[..PersistenceLimits.ReviewNoteBody] : body,
        Truncated = body.Length > PersistenceLimits.ReviewNoteBody, AtTicks = at.UtcTicks,
    };
}
