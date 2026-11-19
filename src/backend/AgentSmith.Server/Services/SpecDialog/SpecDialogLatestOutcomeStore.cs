using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using AgentSmith.Application.Services.Filing;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Keeps the proposal pane's state on the open session, so a page reloaded mid-conversation
/// shows what is being decided and what was filed rather than an empty pane.
/// <para>
/// A write for a thread with no open session changes nothing — the pane has nowhere to show it,
/// and the turn that proposed it must not fail for want of a record. 2026-11-17-a0b6d: the
/// proposals themselves are rows of their own; this reads them and writes only the filing.
/// </para>
/// <para>
/// Nothing here may fail its caller, because its caller is the filing notice (2026-11-10-ccd9: the
/// proposal's own writes moved to <see cref="SpecDialogProposalState"/>, where a failure fails the turn). The filing write runs AFTER the tickets exist: had a transient database error
/// propagated from it, the notice telling the master and the transcript what was filed would
/// never be written, and an operator seeing no confirmation asks again and files everything a
/// second time. A record the pane uses for display is not worth a duplicate ticket, so a failed
/// write is logged and dropped, and a row that cannot be read shows as absent rather than taking
/// the whole dialog read down with it.
/// </para>
/// </summary>
public sealed class SpecDialogLatestOutcomeStore(
    SpecDialogSessionRepository repository, BoundSessionNudge bound, ILogger<SpecDialogLatestOutcomeStore> logger)
{
    // 2026-09-22-9519: a start state this build cannot place costs one word, not the whole filing.
    // In the OPTIONS, never on the property: there it outranks the enum's own and changes the page.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        { Converters = { new TolerantNullableEnumConverter<FiledStartState>() } };

    public Task SetFilingAsync(string platform, string threadId, FilingReport report, OutcomeProposal filed, CancellationToken ct) =>
        UpdateOpenAsync(platform, threadId,
            session => session.LatestFilingJson = JsonSerializer.Serialize(
                new SpecDialogFiling(report.Filed, report.Error, DateTimeOffset.UtcNow,
                    SpecDialogProposalComposer.KindOf(filed), report.Notes), JsonOptions),
            ct);

    public async Task<SpecDialogLatestOutcome> ReadAsync(
        string platform, string threadId, CancellationToken ct)
    {
        var session = await repository.GetOpenByThreadAsync(platform, threadId, ct);
        return session is null ? SpecDialogLatestOutcome.None : await OfAsync(session, ct);
    }

    /// <summary>2026-09-22-9519: the same read, addressed by the SESSION row — what a tool holds, whose conversation
    /// runs on the page or on chat; every other path here is keyed by platform, thread and an open session.</summary>
    public async Task<SpecDialogLatestOutcome> ReadBySessionAsync(string sessionId, CancellationToken ct)
    {
        var session = await repository.GetBySessionIdAsync(sessionId, ct);
        return session is null ? SpecDialogLatestOutcome.None : await OfAsync(session, ct);
    }

    /// <summary>Replaces the filing record on one session row — the WHOLE record, because the caller
    /// changed one ticket's state and the rest of that filing must survive unchanged.</summary>
    public Task SetFilingBySessionAsync(string sessionId, SpecDialogFiling filing, CancellationToken ct) =>
        UpdateAsync(() => repository.GetBySessionIdAsync(sessionId, ct),
            session => session.LatestFilingJson = JsonSerializer.Serialize(filing, JsonOptions), sessionId, ct);

    /// <summary>What a session holds, open or closed: its filing and the proposal awaiting a decision with its id. One
    /// unreadable record shows as absent rather than failing the read.</summary>
    private async Task<SpecDialogLatestOutcome> OfAsync(SpecDialogSession session, CancellationToken ct)
    {
        var awaiting = await repository.Proposals.AwaitingAsync(session.SessionId, ct);
        var proposal = Readable(awaiting?.Json, OutcomeProposalJson.Read, session.ThreadId);
        return new(proposal, FilingOf(session), awaiting?.Id);
    }

    /// <summary>What each of <paramref name="sessions"/> holds, in one read — the conversation list's, with no draft.</summary>
    internal async Task<IReadOnlyDictionary<string, SpecDialogLatestOutcome>> ListedAsync(
        IReadOnlyList<SpecDialogSession> sessions, CancellationToken ct)
    {
        var awaiting = await repository.Proposals.AwaitingBySessionsAsync([.. sessions.Select(s => s.SessionId)], ct);
        return sessions.ToDictionary(s => s.SessionId, s => new SpecDialogLatestOutcome(
            Readable(awaiting.GetValueOrDefault(s.SessionId), OutcomeProposalJson.Read, s.ThreadId), FilingOf(s)), StringComparer.Ordinal);
    }

    /// <summary>The session's latest filing; null when none is, or it cannot be read.</summary>
    internal SpecDialogFiling? FilingOf(SpecDialogSession session) =>
        Readable(session.LatestFilingJson, json => JsonSerializer.Deserialize<SpecDialogFiling>(json, JsonOptions), session.ThreadId);

    private T? Readable<T>(string? json, Func<string, T?> read, string? threadId) where T : class
    {
        if (json is null) return null;
        try
        {
            return read(json);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
            logger.LogWarning(ex, "Spec-dialog pane record on thread {ThreadId} is unreadable — shown as absent", threadId);
            return null;
        }
    }

    private Task UpdateOpenAsync(
        string platform, string threadId, Action<SpecDialogSession> change, CancellationToken ct) =>
        UpdateAsync(() => repository.GetOpenByThreadAsync(platform, threadId, ct), change, threadId, ct);

    private async Task UpdateAsync(
        Func<Task<SpecDialogSession?>> find, Action<SpecDialogSession> change, string named, CancellationToken ct)
    {
        try
        {
            var session = await find();
            if (session is null) return;
            change(session);
            await bound.SaveAsync(session, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Spec-dialog pane record on thread {ThreadId} was not saved", named);
        }
    }
}
