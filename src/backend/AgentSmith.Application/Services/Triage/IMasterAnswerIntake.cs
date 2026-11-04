using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// 2026-09-03-3c07: delivers the answer a resume brought back to the master that asked.
/// </summary>
public interface IMasterAnswerIntake
{
    /// <summary>
    /// The answer the resume delivered for <paramref name="questions"/>, consumed and made
    /// part of the master's input — or null when no delivered answer belongs to this ask.
    /// </summary>
    Task<DialogAnswer?> TryDeliverAsync(
        PipelineContext pipeline, IReadOnlyList<PlanOpenQuestion> questions);

    /// <summary>
    /// 2026-11-02-2b26d: makes <paramref name="answer"/> part of the master's input — merged into the answers an
    /// earlier ask left — and clears the question, so no later step parks on it. Records nothing on the trail:
    /// whoever took the answer records it.
    /// </summary>
    void Accept(PipelineContext pipeline, IReadOnlyList<PlanOpenQuestion> questions, DialogAnswer answer);
}
