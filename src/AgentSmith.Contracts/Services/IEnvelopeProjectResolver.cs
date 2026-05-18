using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Resolves the (project, pipeline) tuples that match an incoming ticket envelope. The
/// p0140a ProjectResolver implementation is stateless and pure over (config, envelope).
/// Naming: "Envelope" distinguishes this from the unrelated Server-side IProjectResolver
/// (ticket-existence API lookup used by IntentEngine) which has a different responsibility
/// and return shape.
/// </summary>
public interface IEnvelopeProjectResolver
{
    IReadOnlyList<ProjectMatch> Resolve(AgentSmithConfig config, IncomingTicketEnvelope envelope);
}
