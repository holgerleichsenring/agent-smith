using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0429: context for the step that accounts for the ratified scan criteria against the
/// execution trail. Carries only the pipeline — the contract and the trail are both on it.
/// </summary>
public sealed record AccountScanCoverageContext(PipelineContext Pipeline) : ICommandContext;
