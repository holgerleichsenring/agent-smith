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
}
