using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Application.Services.Spawning;

/// <summary>What a spawn needs before anything is read: a ticket id, a platform and a project with repositories.
/// 2026-11-27-3532a: lifted out of <see cref="SpawnPipelineRunsUseCase"/>.</summary>
internal static class SpawnEnvelopeCheck
{
    public static void Validate(ResolvedProject project, IncomingTicketEnvelope envelope)
    {
        if (string.IsNullOrEmpty(envelope.TicketId))
            throw new ArgumentException("envelope.TicketId is required for spawn.", nameof(envelope));
        if (string.IsNullOrEmpty(envelope.Platform))
            throw new ArgumentException("envelope.Platform is required for spawn.", nameof(envelope));
        if (project.Repos.Count == 0)
            throw new InvalidOperationException(
                $"Project '{project.Name}' has no repos; cannot spawn pipeline runs.");
    }
}
