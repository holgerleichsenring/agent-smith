using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-06-3d81: the criteria the run declined, as the text the ticket author reads — in
/// the ticket comment, in the pull request body and in result.md, beside the account.
/// <para>
/// A master that judges a criterion impossible in this repository answers not_applicable
/// with the evaluated meaning of not doing it. That answer satisfied the gate and reached
/// nobody: the account is what every surface rendered, and it judges the branch on its own.
/// Nothing is decided here — what the master answered is simply shown.
/// </para>
/// </summary>
public static class DeclinedCriteriaSection
{
    public const string Heading = "## Declined by the run";

    private const string Preamble =
        "The agent judged that no work inside this repository could make these criteria true and "
        + "declined each with the evaluated meaning of not doing it. They are sentences of the ticket, "
        + "not of the change: the delivery account, where one was taken, still judges the branch on "
        + "its own.";

    public static string Build(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var declined = DeclinedCriteriaLedger.Current(pipeline).All;
        return declined.Count == 0 ? string.Empty : "\n\n" + Render(declined).TrimEnd();
    }

    public static string Render(IReadOnlyList<DeclinedCriterion> declined)
    {
        ArgumentNullException.ThrowIfNull(declined);
        if (declined.Count == 0) return string.Empty;
        var lines = new List<string> { Heading, string.Empty, Preamble, string.Empty };
        lines.AddRange(declined.Select(Row));
        return string.Join("\n", lines) + "\n";
    }

    private static string Row(DeclinedCriterion declined) =>
        $"- [~] **{declined.Criterion}** — {declined.Reason}"
        + (declined.PhaseId is null ? string.Empty : $" _(phase {declined.PhaseId})_");
}
