using AgentSmith.Application.Services.Filing;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042em: how a composed filing notice names one ticket: its key (<see cref="FiledTicketKey"/>), with the
/// url behind it in whichever link shape the channel renders. EVERY channel, not just the page — filed tickets read as
/// keys rather than raw urls, and a chat reader deserves the same sentence. A reference that is not a web url is not
/// linked: a link to "#412" navigates nowhere and claims it does. 2026-11-17-5701b: it stayed in the server when the
/// key moved to Application, because the markup is the server's.
/// </summary>
internal static class FiledTicketNaming
{
    internal static string Named(FiledTicket ticket, SpecDialogMarkup markup)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(markup);
        // A filing written before 042em carries no key; its reference is what it always was.
        if (ticket.Key is null) return ticket.Reference;
        return IsWebUrl(ticket.Reference) ? markup.Link(ticket.Key, ticket.Reference) : ticket.Key;
    }

    /// <summary>The same rule DialogFiledPanel applies with /^https?:\/\//, said in this language: an absolute http
    /// or https url and nothing else. A prefix test would admit "httpfoo://x".</summary>
    private static bool IsWebUrl(string reference) =>
        Uri.TryCreate(reference, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
