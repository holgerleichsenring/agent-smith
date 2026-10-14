using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// The ticket steward's master surface, derived from what a bound dialog turn's master is granted. It keeps the
/// reads, the series tools and start_next_attempt, and leaves out what files or authors design work:
/// <c>withdraw_filed_ticket</c> (the steward changes the series with edit_series, it does not withdraw a filing),
/// <c>decide_by_default</c> (the person is in the conversation — a choice is asked, not filed as a pending row)
/// and <c>write_mock</c>/<c>read_mock</c> (mocks belong to the spec the design partner proposes).
/// </summary>
public static class TicketStewardTools
{
    public static readonly IReadOnlySet<string> Excluded = new HashSet<string>(StringComparer.Ordinal)
    {
        "withdraw_filed_ticket", DefaultDecisionToolHost.Name, "write_mock", "read_mock",
    };

    public static IList<AITool> Of(IList<AITool> masterSurface)
    {
        ArgumentNullException.ThrowIfNull(masterSurface);
        return [.. masterSurface.Where(tool => !Excluded.Contains(tool.Name))];
    }
}
