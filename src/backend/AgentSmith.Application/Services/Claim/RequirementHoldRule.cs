using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-11-23-31dca: the start hold of one dependent under <c>requires_start: on_green_push</c>, from the reads of
/// <see cref="GreenReads"/> — moved out of <see cref="UnmetTicketRequirements"/>, which reads the edges and landings for
/// many dependents and asks this rule for each one with an unlanded requirement.
/// </summary>
internal sealed class RequirementHoldRule
{
    /// <summary>The hold of one dependent with unlanded requirements: a prerequisite that is not green holds it if it never
    /// started; a green one stacks it per repo, and only a repo where two independent ones worked holds it — until the merges
    /// that leave one base there (6d85d). A dependent that started is never held again.</summary>
    public async Task<RequirementHold> HoldAsync(
        GreenReads reads, IReadOnlyList<TicketRequirementEdge> unmet, bool wasStarted, CancellationToken ct)
    {
        if (!wasStarted && await WaitingAsync(reads, unmet, ct) is { Count: > 0 } waiting) return new(true, waiting, GreenLifts: true);
        var stack = await reads.StackPerRepoAsync(unmet, ct);
        return wasStarted || stack.Merges.Count == 0 ? new(true, [], GreenLifts: true) { StackedPerRepo = stack.Bases }
            : new(true, unmet, GreenLifts: true) { MergesToStart = await WithBasesAsync(reads, stack.Merges, ct) };
    }

    // What a dependent that never started waits for: its prerequisites when one is not green (as before), else the deeper ones
    // of their chain that are not green — any of them may be a repo's base, and a base is a green branch.
    private static async Task<IReadOnlyList<TicketRequirementEdge>> WaitingAsync(
        GreenReads reads, IReadOnlyList<TicketRequirementEdge> unmet, CancellationToken ct)
    {
        if ((await reads.NotGreenAsync(unmet, ct)).Count > 0) return unmet;
        var direct = unmet.Select(e => e.RequiredTicketKey).ToHashSet(StringComparer.Ordinal);
        return await reads.NotGreenAsync([.. (await reads.UnlandedChainAsync(unmet, ct)).Where(e => !direct.Contains(e.RequiredTicketKey))], ct);
    }

    // 2026-11-22-6d85d: a named prerequisite stacked on an unlanded ticket lands only with that base, so its base's merge is
    // named too, first — 2026-11-23-97c2b: the chain walked by the reads, landings read once.
    private static async Task<IReadOnlyList<TicketRequirementEdge>> WithBasesAsync(
        GreenReads reads, IReadOnlyList<TicketRequirementEdge> merges, CancellationToken ct)
    {
        var chain = await reads.UnlandedChainAsync(merges, ct);
        return [.. chain.Skip(merges.Count).Reverse(), .. merges];
    }
}
