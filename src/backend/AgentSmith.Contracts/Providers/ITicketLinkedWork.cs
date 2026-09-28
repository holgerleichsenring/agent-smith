using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-09-28-1da5c: what a TRACKER shows against one ticket — the pull requests it links, and the
/// branches where it links those too.
/// <para>
/// Asked whether a ticket's branches were finished, a design conversation answered that it could
/// not verify: every link demanded authentication and returned a redirect to sign-in. That answer
/// was correct. The defect is that it was the only one available — the conversation's single reach
/// outward is a request tool holding no credential, while the same turn reads the ticket itself
/// through a port whose factory resolved a token.
/// </para>
/// <para>
/// A PORT RATHER THAN A SECRET IN THE MODEL'S HANDS. A request tool carrying a personal access
/// token could reach everything that token reaches, including writes, on a surface whose own
/// comment says it performs no run and no write. This answers named questions and cannot be talked
/// into a PUT.
/// </para>
/// </summary>
public interface ITicketLinkedWork
{
    /// <summary>
    /// What the tracker links to this ticket, or a refusal carrying its reason. Never throws: a
    /// tracker that cannot be asked is an answer, not an exception.
    /// </summary>
    Task<TicketLinkedWorkResult> ForAsync(TicketId ticketId, CancellationToken cancellationToken);
}
