using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for a pr-review skill round (no ticket — the PR diff is the
/// subject under review).
/// </summary>
public sealed record PrReviewSkillRoundContext(
    string SkillName,
    int Round,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
