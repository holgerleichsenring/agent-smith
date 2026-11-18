using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// p0341c: the master loop's in-pass governor — a <see cref="DelegatingChatClient"/> that
/// sits BELOW UseFunctionInvocation, so the FunctionInvokingChatClient re-enters it on
/// every tool iteration:
/// <list type="bullet">
///   <item>WITHIN-pass money fence — before each iteration it consults the live budget;
///     an exhausted budget throws <see cref="MasterBudgetExhaustedException"/> so a single
///     runaway pass stops on money, not only the 200-iteration anti-runaway ceiling. After
///     each iteration it feeds the response usage back to the budget estimator.</item>
///   <item>Ledger reminder — after N iterations WITHOUT an <c>update_progress</c> call
///     (p0359: staleness, not a blunt modulo — a disciplined model is never nagged), it
///     appends the current ledger as a synthetic user message, re-surfacing the master's
///     own checklist into the running conversation.</item>
/// </list>
/// 2026-11-18-9c1cc: no drift trigger and no ledger-complete brake — nothing in the loop judges
/// whether the master reads too much or should have finished; money and its own checklist are all.
/// Stateless config, per-pass counters — one instance is built per <c>Create</c> call.
/// </summary>
public sealed class MasterLoopGovernorChatClient(IChatClient inner, MasterLoopHooks hooks)
    : DelegatingChatClient(inner)
{
    private const string LedgerToolName = "update_progress";

    private int _sinceLedgerUpdate;

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // The within-pass money fence — checked BEFORE spending another iteration.
        if (hooks.IsBudgetExhausted?.Invoke() == true)
            // 2026-09-22-7c41a: RenderBudgetStop names the cap and the spend that crossed it.
            throw new MasterBudgetExhaustedException(
                (hooks.RenderBudgetStop?.Invoke() ?? "The per-pipeline cost budget was exhausted")
                + " mid-pass — the coding loop was stopped on money (not the anti-runaway "
                + "iteration ceiling). Partial work is preserved and the run is recorded as "
                + "cost-cap-exhausted.");

        var list = messages as IList<ChatMessage> ?? messages.ToList();
        if (TouchedLedger(list)) _sinceLedgerUpdate = 0;
        _sinceLedgerUpdate++;

        var injected = Stale() ? hooks.RenderReminder?.Invoke() : null;
        if (!string.IsNullOrWhiteSpace(injected))
        {
            list = new List<ChatMessage>(list) { new(ChatRole.User, injected) };
            _sinceLedgerUpdate = 0;
        }

        var response = await base.GetResponseAsync(list, options, cancellationToken);
        hooks.RecordIterationUsage?.Invoke(response);
        return response;
    }

    // N iterations without an update_progress call; N<=0 disables the reminder.
    private bool Stale() =>
        hooks.ReminderEveryNIterations > 0 && _sinceLedgerUpdate >= hooks.ReminderEveryNIterations;

    // Whether the latest assistant turn called update_progress.
    private static bool TouchedLedger(IList<ChatMessage> messages) =>
        messages.LastOrDefault(m => m.Role == ChatRole.Assistant) is { } last
        && last.Contents.OfType<FunctionCallContent>()
            .Any(c => string.Equals(c.Name, LedgerToolName, StringComparison.OrdinalIgnoreCase));
}
