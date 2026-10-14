using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Whether tickets a proposed list requires as <c>ticket:&lt;id&gt;</c> exist on the project's tracker. Read when the
/// list is reviewed and again when it is filed, one classification for both: a tracker's own not-found — Azure
/// DevOps' TF401232 included — is missing, any other failure is unreadable with its class and never the SDK's text.
/// </summary>
public interface ITicketExistence
{
    Task<TicketExistenceResult> CheckAsync(ResolvedProject project, IReadOnlyList<string> ids, CancellationToken ct);
}
