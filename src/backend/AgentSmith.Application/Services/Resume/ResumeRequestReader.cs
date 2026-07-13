using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Resume;

/// <summary>
/// p0327: detects a resume launch (ContextKeys.ResumeCheckpoint riding the
/// request context), rehydrates the checkpointed pipeline context ON TOP of the
/// standard seeding (restored run state like a narrowed repo list wins), stages
/// the delivered answer for the ask gate, and returns the step cursor to
/// re-enter at. A malformed payload fails fast — silently restarting the whole
/// pipeline would repeat side effects the checkpointed half already performed.
/// </summary>
public sealed class ResumeRequestReader(
    IPipelineContextSerializer contextSerializer,
    ILogger<ResumeRequestReader> logger)
{
    public ResumeExecutionPlan? TryRead(PipelineContext pipeline)
    {
        var payloadJson = ReadPayloadJson(pipeline);
        if (payloadJson is null) return null;

        var payload = JsonSerializer.Deserialize<ResumePayload>(payloadJson)
            ?? throw new InvalidOperationException("Resume payload deserialized to null.");
        if (payload.Commands.Count == 0)
            throw new InvalidOperationException("Resume payload carries no remaining commands.");

        contextSerializer.Restore(payload.ContextJson, pipeline);
        StageAnswer(pipeline, payload);

        var commands = payload.Commands.Select(c => c.ToPipelineCommand()).ToList();
        logger.LogInformation(
            "Resuming checkpointed run at '{Head}' with {Count} remaining commands (execution count {ExecutionCount})",
            commands[0].DisplayName, commands.Count, payload.ExecutionCount);
        return new ResumeExecutionPlan(commands, payload.ExecutionCount);
    }

    private static void StageAnswer(PipelineContext pipeline, ResumePayload payload)
    {
        var answer = JsonSerializer.Deserialize<DialogAnswer>(payload.AnswerJson)
            ?? throw new InvalidOperationException("Resume payload carries no answer.");
        pipeline.Set(ContextKeys.ResumedDialogueAnswer, answer);

        var question = JsonSerializer.Deserialize<DialogQuestion>(payload.QuestionJson);
        if (question is not null) pipeline.Set(ContextKeys.DialogueQuestion, question);
    }

    // The payload rides the request context as a plain string, but the Redis
    // job queue's JSON round-trip re-materializes request values as JsonElement.
    private static string? ReadPayloadJson(PipelineContext pipeline)
    {
        if (!pipeline.Has(ContextKeys.ResumeCheckpoint)) return null;
        if (pipeline.TryGet<string>(ContextKeys.ResumeCheckpoint, out var text) && text is not null)
            return text;
        if (pipeline.TryGet<JsonElement>(ContextKeys.ResumeCheckpoint, out var element)
            && element.ValueKind == JsonValueKind.String)
            return element.GetString();
        throw new InvalidOperationException("ResumeCheckpoint is present but not a string payload.");
    }
}
