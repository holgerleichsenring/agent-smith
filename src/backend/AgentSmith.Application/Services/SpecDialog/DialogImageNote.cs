namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-20-3af8: what a design turn is told about the images on its conversation — beside
/// the reply contract, because that is where a turn reads what it has been given.
/// <para>
/// THREE BRANCHES, one of which the ticket path has no use for. A ticket is read once, under a
/// ceiling nothing reaches; a conversation accumulates, so a turn can carry FEWER than exist and
/// the operator whose early diagram dropped out has to be told. The ticket section is not
/// reused: its "## Ticket attachments" heading has no business in a design conversation, and its
/// sentences name a ticket a conversation does not have.
/// </para>
/// </summary>
public static class DialogImageNote
{
    public static string Render(int existing, int carried)
    {
        if (existing == 0) return string.Empty;
        if (carried == 0)
            return $"{existing} image(s) are attached to this conversation but are NOT viewable "
                + "by this model. Ask the operator what they show if they look essential.";
        return carried >= existing
            ? $"{existing} image(s) from this conversation are attached to this message as image content."
            : $"{existing} image(s) are attached to this conversation; the {carried} most recent "
                + "of them ride this message as image content. Ask the operator to re-attach an "
                + "earlier one if you need it.";
    }
}
