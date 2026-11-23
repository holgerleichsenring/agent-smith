using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-23-933db: context for the PocIntake step — the PoC id, revision and actor the server seeded, null
/// when a seed is missing or not a number.</summary>
public sealed record PocIntakeContext(
    PipelineContext Pipeline, ResolvedProject Project, long? PocId, int? Revision, string? Actor) : ICommandContext;
