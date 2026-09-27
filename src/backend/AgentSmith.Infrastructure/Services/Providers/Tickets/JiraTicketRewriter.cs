using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-25-8e51e: Jira's answer to a description rewrite is NO, and the reason is the round
/// trip rather than a permission.
/// <para>
/// A Jira description is stored as an Atlassian Document Format tree and read back by
/// <see cref="JiraAdfParser"/>, which collects <c>text</c> nodes and nothing else. Mentions,
/// emoji, media, inline cards and status nodes are dropped, a link keeps its label and loses its
/// target, and every mark, heading level, list marker and table structure is flattened to bare
/// lines. A rewrite is READ-MODIFY-WRITE by construction — the framework's region has to be found
/// inside what a person also writes in — so on Jira it would send back a description with the
/// human half of it destroyed, and the destruction would be silent.
/// </para>
/// <para>
/// It exists as a class rather than as a missing registration so the SENTENCE ABOVE reaches the
/// operator. "No rewriter is wired for this tracker" is a fact about us; this is the fact about
/// Jira, and it is the one that tells them to edit the ticket themselves.
/// </para>
/// </summary>
public sealed class JiraTicketRewriter : ITicketRewriter
{
    /// <summary>What an operator is told, once, wherever the refusal surfaces.</summary>
    public const string Reason =
        "Jira stores a description as a structured document and hands it back as plain text only: "
        + "mentions, emoji, media, inline cards and status disappear, links lose their target, and "
        + "headings, lists, tables and every mark are flattened. Rewriting it would destroy the "
        + "half of the description a person wrote, so agent-smith does not write it. The approved "
        + "specification is stored and on the ticket branch; paste the part you want into the "
        + "ticket yourself.";

    public Task<TicketRewriteResult> RewriteRegionAsync(
        TicketId ticketId, string region, CancellationToken cancellationToken) =>
        Task.FromResult(TicketRewriteResult.Unsupported(Reason));
}
