using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// One tracker ticket created while filing a confirmed outcome. <see cref="Reference"/> is the
/// display form — a web url where the tracker gives one — so the ID is carried beside it:
/// 2026-09-17-042em formats the display key from it, 2026-09-17-042ej reads the ticket's runs by
/// it, and 2026-09-17-0e79a keys the approved set by it, and none of the three can recover it
/// from a url without a parser per provider.
/// </summary>
public sealed record FiledTicket(string Reference, string Title)
{
    /// <summary>The tracker-native id, and the project it was filed into. Both null on a filing
    /// written before 2026-09-17-042eg, which reads as unknown rather than as a wrong answer.</summary>
    public string? TicketId { get; init; }

    /// <inheritdoc cref="TicketId"/>
    public string? Project { get; init; }

    /// <summary>What the ticket became — started, or not started with its reason. A filing stored
    /// before 2026-09-22-b3d7 may also read back <see cref="FiledStartState.Record"/>, which
    /// nothing writes any more.</summary>
    public FiledWorkStart? Start { get; init; }

    /// <summary>2026-09-17-042em: what a person calls this ticket — the Jira key, or the tracker's
    /// number behind a hash. Null on a filing written before that phase, which then reads by its
    /// <see cref="Reference"/> exactly as it always did.</summary>
    public string? Key { get; init; }

    /// <summary>The id, the project and the display key travel on the report: a Reference is a
    /// web url where the tracker gives one, and runs are found by project and id.</summary>
    public static FiledTicket Of(CreatedTicket created, string title, ResolvedProject project) =>
        new(created.Reference, title)
        { TicketId = created.Id.Value, Project = project.Name, Key = FiledTicketKey.Of(project, created) };
}

/// <summary>
/// p0315c: exactly what a filing attempt did. Filed lists every ticket that
/// WAS created (in creation order) even when Error is set — a partial epic
/// must never silently lose children, so the report is honest about both
/// halves.
/// </summary>
public sealed record FilingReport(IReadOnlyList<FiledTicket> Filed, string? Error)
{
    public bool Succeeded => Error is null;

    /// <summary>
    /// What went wrong without unfiling anything. Never an error: every ticket named in
    /// <see cref="Filed"/> exists.
    /// <para>
    /// 2026-09-22-b3d7: NO WRITER LEFT. Its only one was the slice-record filer — a record that
    /// was not created, a parent link that did not land — and a filing is now one ticket, which
    /// either exists or is the filing's error. It stays read-only, because a filing stored before
    /// this phase carries notes the pane still shows.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}
