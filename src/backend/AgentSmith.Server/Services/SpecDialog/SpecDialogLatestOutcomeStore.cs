using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Keeps the proposal pane's state on the open session, so a page reloaded mid-conversation
/// shows what is being decided and what was filed rather than an empty pane.
/// <para>
/// Not the confirmed-outcome handoff: that column means "approved and not fully filed", and
/// this one means "what the pane shows". A write for a thread with no open session changes
/// nothing — the pane has nowhere to show it, and the turn that proposed it must not fail
/// for want of a record.
/// </para>
/// <para>
/// Nothing here may fail its caller, because its callers are the approval question and the
/// filing notice. The filing write runs AFTER the tickets exist: had a transient database error
/// propagated from it, the notice telling the master and the transcript what was filed would
/// never be written, and an operator seeing no confirmation asks again and files everything a
/// second time. A record the pane uses for display is not worth a duplicate ticket, so a failed
/// write is logged and dropped, and a row that cannot be read shows as absent rather than taking
/// the whole dialog read down with it.
/// </para>
/// </summary>
public sealed class SpecDialogLatestOutcomeStore(
    SpecDialogSessionRepository repository, ILogger<SpecDialogLatestOutcomeStore> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task SetProposalAsync(
        string platform, string threadId, OutcomeProposal proposal, CancellationToken ct) =>
        UpdateOpenAsync(platform, threadId,
            session => session.LatestProposalJson = OutcomeProposalJson.Write(proposal), ct);

    /// <summary>A rejected or timed-out proposal is no longer under discussion; a reload must
    /// not offer an approval nobody is waiting for.</summary>
    public Task ClearProposalAsync(string platform, string threadId, CancellationToken ct) =>
        UpdateOpenAsync(platform, threadId, session => session.LatestProposalJson = null, ct);

    public Task SetFilingAsync(
        string platform, string threadId, FilingReport report, OutcomeProposal filed,
        CancellationToken ct) =>
        UpdateOpenAsync(platform, threadId,
            session => session.LatestFilingJson = JsonSerializer.Serialize(
                new SpecDialogFiling(report.Filed, report.Error, DateTimeOffset.UtcNow,
                    SpecDialogProposalComposer.KindOf(filed), report.Notes), JsonOptions),
            ct);

    public async Task<SpecDialogLatestOutcome> ReadAsync(
        string platform, string threadId, CancellationToken ct)
    {
        var session = await repository.GetOpenByThreadAsync(platform, threadId, ct);
        return session is null ? SpecDialogLatestOutcome.None : Of(session);
    }

    /// <summary>
    /// What a session holds, open or closed — the one reading of the two columns, used by the
    /// view and by the conversation list alike. Forgiving for both: one unreadable row shows as
    /// absent rather than failing the whole dialog read, or the whole list.
    /// </summary>
    internal SpecDialogLatestOutcome Of(SpecDialogSession session) => new(
        Readable(session.LatestProposalJson, OutcomeProposalJson.Read, session.ThreadId),
        Readable(session.LatestFilingJson,
            json => JsonSerializer.Deserialize<SpecDialogFiling>(json, JsonOptions), session.ThreadId));

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

    private async Task UpdateOpenAsync(
        string platform, string threadId, Action<SpecDialogSession> change, CancellationToken ct)
    {
        try
        {
            var session = await repository.GetOpenByThreadAsync(platform, threadId, ct);
            if (session is null) return;
            change(session);
            await repository.SaveAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Spec-dialog pane record on thread {ThreadId} was not saved", threadId);
        }
    }
}
