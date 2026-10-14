using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762g: what every spec's review in one step shares — the run, its settings read once,
/// the agent the calls are made under, and the wider goal each reviewer is shown.</summary>
public sealed record SpecReviewRun(
    PipelineContext Pipeline, SpecReviewSettings Settings, AgentConfig Agent, string? WiderGoal)
{
    /// <summary>2026-10-13-8762h: the run's question pin, handed to every reviewer call.</summary>
    public string? QuestionPin => Pipeline.TryGet<string>(ContextKeys.SpecQuestionPin, out var pin) ? pin : null;
}
