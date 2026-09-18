using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services.SpecDialog;

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
/// filers one construction site (<see cref="OutcomeTicketFiler.Entry"/>): there is exactly one
/// switch either way, and the one here is total over <see cref="TrackerType"/> instead of an
/// optional field a provider can forget, which would degrade to null with nothing to say so.
/// The hash convention is already the domain's, not the providers' — <see
/// cref="CreatedTicket.Reference"/> falls back to it — so this is where it belongs.
/// </para>
/// </summary>
internal static class FiledTicketKey
{
    /// <summary>The display key of a ticket just filed into <paramref name="project"/>'s tracker.</summary>
    internal static string Of(ResolvedProject project, CreatedTicket created) =>
        Of(project.Tracker.Type, created.Id.Value);

    /// <summary>
    /// A Jira id IS the key its people use ("SAMPLE-412"); every other tracker returns a bare
    /// number, which reads as a ticket only behind the hash <see cref="CreatedTicket.Reference"/>
    /// already falls back to. A tracker added later degrades that way rather than into a fake key.
    /// </summary>
    internal static string Of(TrackerType tracker, string id) =>
        tracker == TrackerType.Jira ? id : $"#{id}";

    /// <summary>
    /// How a composed filing notice names one ticket: the key, with the url behind it in whichever
    /// link shape the channel renders. EVERY channel, not just the page — the phase's own claim is
    /// that filed tickets read as keys rather than raw urls, and a chat reader deserves the same
    /// sentence. A reference that is not a web url is not linked: a link to "#412" navigates
    /// nowhere and claims it does.
    /// </summary>
    internal static string Named(FiledTicket ticket, SpecDialogMarkup markup)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(markup);
        // A filing written before this phase carries no key; its reference is what it always was.
        if (ticket.Key is null) return ticket.Reference;
        return IsWebUrl(ticket.Reference) ? markup.Link(ticket.Key, ticket.Reference) : ticket.Key;
    }

    /// <summary>
    /// The same rule DialogFiledPanel applies with /^https?:\/\//, said in this language: an
    /// absolute http or https url and nothing else. A prefix test would admit "httpfoo://x".
    /// </summary>
    private static bool IsWebUrl(string reference) =>
        Uri.TryCreate(reference, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
