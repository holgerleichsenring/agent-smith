namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>2026-11-17-e061a: preset name of a close — a person's close or a conversation's withdrawal.</summary>
    public const string CloseName = "close";

    /// <summary>2026-11-17-e061a: preset name of a reopen, which clears the close mark.</summary>
    public const string ReopenName = "reopen";

    // 2026-11-17-e061a: launched by the server with a request and an outcome slot; no catalog read, no sandbox, no master.
    public static readonly IReadOnlyList<string> Close = [CommandNames.CloseTicket];

    public static readonly IReadOnlyList<string> Reopen = [CommandNames.ReopenTicket];

    /// <summary>2026-11-17-e061b: preset name of a start — a landed requirement's dependent, an approved never-worked ticket.</summary>
    public const string StartName = "start";

    public static readonly IReadOnlyList<string> Start = [CommandNames.StartTicket];

    /// <summary>2026-11-17-e061c: preset name of an amendment — an approval in a never-worked ticket's own conversation.</summary>
    public const string AmendName = "amend";

    public static readonly IReadOnlyList<string> Amend = [CommandNames.AmendTicket];

    /// <summary>2026-11-17-e061c: preset name of agent-smith's comments on tickets — an approved comment, a follow-up's link.</summary>
    public const string CommentName = "comment";

    public static readonly IReadOnlyList<string> Comment = [CommandNames.PostTicketComments];

    /// <summary>2026-11-17-dd05b: preset name of a body render — the ticket's body re-rendered from the specs on its branch.</summary>
    public const string TicketBodyName = "ticket-body";

    public static readonly IReadOnlyList<string> TicketBody = [CommandNames.RenderTicketBody];

    /// <summary>2026-11-17-9a39c: preset name of a retarget — a stacked dependent's pull requests leave a landed branch.</summary>
    public const string RetargetName = "retarget";

    public static readonly IReadOnlyList<string> Retarget = [CommandNames.RetargetPullRequests];

    /// <summary>2026-11-17-3435b: preset name of a CI-green merge — a gate-passed attempt's pull requests merged once the
    /// host's CI is green on every one (merge: on_ci_green). The server's CI watch starts it with the ticket's lease.</summary>
    public const string MergeGreenName = "merge-green";

    public static readonly IReadOnlyList<string> MergeGreen = [CommandNames.CompletePullRequests];

    // 2026-11-17-e061: every act on a filed ticket, by preset — the static constructor adds them to the presets, and
    // routing reads it to keep them off a label (a launch with a server seed, never a ticket's route). Declared after
    // the step lists above, which a field initializer in this file reads in textual order.
    private static readonly Dictionary<string, IReadOnlyList<string>> HostActSteps = new(StringComparer.OrdinalIgnoreCase)
    {
        [CloseName] = Close, [ReopenName] = Reopen, [StartName] = Start, [AmendName] = Amend, [CommentName] = Comment,
        [TicketBodyName] = TicketBody, // 2026-11-17-dd05b
        [RetargetName] = Retarget, // 2026-11-17-9a39c
        [MergeGreenName] = MergeGreen, // 2026-11-17-3435b
    };
}
