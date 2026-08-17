using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0429: context for the step that states what the scan is looking for, before the
/// first scanner runs. Carries only the pipeline — the pipeline name is what the
/// contract is derived from.
/// </summary>
public sealed record RatifyScanContractContext(PipelineContext Pipeline) : ICommandContext;
