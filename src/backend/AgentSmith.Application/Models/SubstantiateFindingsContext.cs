using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0429: context for the step that puts every unvouched finding to a fresh instance.
/// Carries only the pipeline — the findings, the sandbox and the agent are all on it.
/// </summary>
public sealed record SubstantiateFindingsContext(PipelineContext Pipeline) : ICommandContext;
