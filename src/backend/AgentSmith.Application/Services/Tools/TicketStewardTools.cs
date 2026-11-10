using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// The ticket steward's master surface, derived from what a bound dialog turn's master is granted. It keeps the
/// reads, the series tools and start_next_attempt, and leaves out what files or authors design work:
/// <c>withdraw_filed_ticket</c> (the steward changes the series with edit_series, it does not withdraw a filing),
/// <c>write_mock</c>/<c>read_mock</c> (mocks belong to the spec the design partner proposes) and
/// <c>state_work_picture</c> (2026-11-05-862ea: the picture frames work the design partner proposes) and
/// <c>outcome_format</c> (2026-11-05-862eb: the formats are the design partner's; the steward's own frame carries its own).
/// </summary>
public static class TicketStewardTools
{
    public static readonly IReadOnlySet<string> Excluded = new HashSet<string>(StringComparer.Ordinal)
    {
        "withdraw_filed_ticket", "write_mock", "read_mock", WorkPictureToolHost.Name,
        OutcomeFormatToolHost.Name,
    };

    public static IList<AITool> Of(IList<AITool> masterSurface)
    {
        ArgumentNullException.ThrowIfNull(masterSurface);
        return [.. masterSurface.Where(tool => !Excluded.Contains(tool.Name))];
    }
}
