using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0147f: maps a provider-specific raw payload (e.g. Octokit <c>Issue</c>,
/// Jira/GitLab REST JSON, Azure DevOps work-item fields) onto the canonical
/// <see cref="Ticket"/> domain entity.
/// <para>
/// Per-platform implementations live next to each provider in
/// <c>AgentSmith.Infrastructure.Services.Providers.Tickets</c>. Each mapper
/// is stateless and pure; it owns the platform's JSON / DTO shape so the
/// provider class can stay an HTTP/SDK orchestrator.
/// </para>
/// </summary>
/// <typeparam name="TRaw">
/// The provider-specific raw payload type: <c>JsonElement</c> for REST
/// providers, <c>Octokit.Issue</c> for the GitHub SDK,
/// <c>IDictionary&lt;string,object&gt;</c> for ADO work-item fields.
/// </typeparam>
public interface ITicketFieldMapper<in TRaw>
{
    /// <summary>
    /// Maps a single raw payload to a <see cref="Ticket"/>.
    /// </summary>
    Ticket Map(TicketId ticketId, TRaw raw);
}
