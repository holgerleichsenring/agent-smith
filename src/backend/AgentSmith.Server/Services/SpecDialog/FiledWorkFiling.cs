namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ej: what the conversation open on a dialog id last filed. The read and the
/// watch both start here, and both start SERVER-SIDE: a caller names a dialog id and never a
/// ticket, so nobody can follow a ticket by asking for it.
/// <para>
/// Only the LATEST filing, and only while the conversation is open — <c>SetFilingAsync</c>
/// overwrites the column on the open session and nothing keeps the ones before it. Keeping
/// every filing is a successor, named by what it does: a conversation keeps each filing it made.
/// </para>
/// </summary>
public sealed class FiledWorkFiling(SpecDialogLatestOutcomeStore latestOutcome)
{
    private const string Platform = DispatcherDefaults.PlatformDashboard;

    /// <summary>The latest filing, or null for a dialog id with no open session.</summary>
    public async Task<SpecDialogFiling?> OfAsync(string dialogId, CancellationToken ct) =>
        (await latestOutcome.ReadAsync(Platform, dialogId, ct)).Filing;

    /// <summary>
    /// The tracker-native ids that filing created. Empty without an open session, and empty
    /// for a filing written before 2026-09-17-042eg, which carries no ids to watch.
    /// </summary>
    public async Task<IReadOnlyList<string>> TicketIdsAsync(string dialogId, CancellationToken ct)
    {
        var filing = await OfAsync(dialogId, ct);
        return filing is null
            ? []
            : [.. filing.Filed.Select(t => t.TicketId).OfType<string>().Distinct(StringComparer.Ordinal)];
    }
}
