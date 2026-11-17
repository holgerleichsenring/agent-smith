using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Filing;

/// <summary>
/// 2026-09-17-042em: the name a person calls a filed ticket by. A filing report carried the
/// Reference, which is a web URL wherever the tracker gives one, so a list of what was just filed
/// read as a column of urls that differ in their last few digits — the one part of them nobody
/// reads. The KEY is what people say to each other and what they type into a tracker's search.
/// <para>
/// FORMATTED HERE, FROM THE ID 2026-09-17-042eg PUT ON THE REPORT, rather than set by each
/// provider onto <see cref="CreatedTicket"/>. The spec proposed the provider because "only the
/// provider knows its tracker's key shape", on the premise that formatting it in the filers would
/// put a tracker switch in each filer. That premise stopped holding when 042eg gave all three
/// filers one construction site (<see cref="FiledTicket.Of"/>): there is exactly one
/// switch either way, and the one here is total over <see cref="TrackerType"/> instead of an
/// optional field a provider can forget, which would degrade to null with nothing to say so.
/// The hash convention is already the domain's, not the providers' — <see
/// cref="CreatedTicket.Reference"/> falls back to it — so this is where it belongs.
/// </para>
/// <para>2026-11-17-5701b: moved to Application with the filing; how a notice LINKS a key (FiledTicketNaming) stays
/// in the server, which owns the chat markup.</para>
/// </summary>
public static class FiledTicketKey
{
    /// <summary>The display key of a ticket just filed into <paramref name="project"/>'s tracker.</summary>
    public static string Of(ResolvedProject project, CreatedTicket created) =>
        Of(project.Tracker.Type, created.Id.Value);

    /// <summary>
    /// A Jira id IS the key its people use ("SAMPLE-412"); every other tracker returns a bare
    /// number, which reads as a ticket only behind the hash <see cref="CreatedTicket.Reference"/>
    /// already falls back to. A tracker added later degrades that way rather than into a fake key.
    /// </summary>
    public static string Of(TrackerType tracker, string id) =>
        tracker == TrackerType.Jira ? id : $"#{id}";
}
