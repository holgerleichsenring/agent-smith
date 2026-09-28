using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-27-481be: saying that a ticket is being read, while it is being read.
/// <para>
/// A bound conversation makes TWO tracker reads — the one that grounds it, and a per-turn check
/// for whether the ticket has moved since. Both were pauses with nothing on screen to account for
/// them, while the repositories opened beside them got credit for theirs.
/// </para>
/// <para>
/// A conversation on any other platform reports NOTHING: its thread id is a dialog id nobody has
/// joined, which is the same rule the observer applies to repository reads.
/// </para>
/// </summary>
public sealed class TicketReadReports(DashboardReadingChannel reading)
{
    /// <summary>Runs a read, saying so before and after — and saying it failed when it did.</summary>
    public async Task<T> AroundAsync<T>(
        string? dialogId, string ticketId, Func<Task<T>> read, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(read);
        await SayAsync(dialogId, ticketId, "opening", ct);
        try
        {
            var answer = await read();
            await SayAsync(dialogId, ticketId, "ready", ct);
            return answer;
        }
        catch
        {
            await SayAsync(dialogId, ticketId, "failed", ct);
            throw;
        }
    }

    private Task SayAsync(string? dialogId, string ticketId, string state, CancellationToken ct) =>
        dialogId is null
            ? Task.CompletedTask
            : reading.PushAsync(dialogId, SpecDialogReadingKinds.Ticket, ticketId, state, ct);
}
