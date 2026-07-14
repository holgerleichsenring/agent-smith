namespace AgentSmith.Application.Models;

/// <summary>
/// p0327: everything a resume launch carries through the capacity queue and the
/// Redis job queue into ExecutePipelineUseCase — the checkpointed step cursor,
/// the serialized pipeline context, and the operator's answer. Rides as a plain
/// JSON string under ContextKeys.ResumeCheckpoint so the request-context
/// JsonElement round-trip cannot mangle it.
/// </summary>
public sealed record ResumePayload(
    IReadOnlyList<CheckpointCommand> Commands,
    string ContextJson,
    int ExecutionCount,
    string QuestionJson,
    string AnswerJson);
