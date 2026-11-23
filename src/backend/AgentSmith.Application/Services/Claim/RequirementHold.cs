using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-11-17-9a39a: what keeps a ticket from its start. <see cref="Unmet"/> holds it; 2026-11-23-31dca:
/// <see cref="StackedPerRepo"/> maps each repo to the prerequisite whose pushed branch it starts on there — a repo with no
/// entry starts on its own base. <see cref="GreenLifts"/> says
/// a green pushed branch would have lifted a requirement — the project stacks and the green read is composed.
/// <para>2026-11-22-6d85d: <see cref="MergesToStart"/> names the merges after which a held ticket may start, when every
/// unlanded prerequisite is green but none is a tip; empty otherwise.</para>
/// </summary>
public sealed record RequirementHold(
    bool HasRequirements, IReadOnlyList<TicketRequirementEdge> Unmet, bool GreenLifts = false)
{
    public IReadOnlyDictionary<string, string> StackedPerRepo { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<TicketRequirementEdge> MergesToStart { get; init; } = [];
}
