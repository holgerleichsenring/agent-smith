using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0277: context for the MergeMasterFindings step that sits between AgenticMaster and
/// DeliverFindings on the security-scan pipeline. Carries only the pipeline — the
/// master's answer + skill name + the raw scanner observations are read from context
/// keys prior steps published.
/// </summary>
public sealed record MergeMasterFindingsContext(
    PipelineContext Pipeline) : ICommandContext;
