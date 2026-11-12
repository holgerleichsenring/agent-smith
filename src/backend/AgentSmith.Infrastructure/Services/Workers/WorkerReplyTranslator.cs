using System.Text.Json;
using AgentSmith.Contracts.Models.Workers;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: turns a worker's reply back into the <see cref="ChatResponse"/> the agent loop
/// consumes unchanged — assistant text and tool calls the FunctionInvokingChatClient then
/// executes for real. A refusal or an empty answer is a failure with a reason, because the
/// worker did not answer the call it was given. 2026-11-12-0c24: an imperfect tool CALL is
/// data — arguments that are not an object arrive empty, so binding answers the model with the
/// parameters it is missing, and a name never offered reaches the loop, which answers it as
/// unknown. The model reads why, and the loop's own tolerance decides when failures end it.
/// </summary>
public sealed class WorkerReplyTranslator
{
    public bool TryTranslate(
        WorkerReply reply, WorkerRequest request, out ChatResponse response, out string? problem)
    {
        var calls = reply.ToolCalls ?? [];
        problem = Validate(reply, calls);
        response = problem is null ? BuildResponse(reply, calls, request) : new ChatResponse();
        return problem is null;
    }

    private static string? Validate(WorkerReply reply, IReadOnlyList<WorkerToolCall> calls)
    {
        if (!string.IsNullOrWhiteSpace(reply.Error))
            return $"the worker refused the call: {reply.Error}";
        if (string.IsNullOrWhiteSpace(reply.Text) && calls.Count == 0)
            return "the worker answered with neither text nor tool calls";
        return null;
    }

    /// <summary>
    /// p0426: the shape of SILENCE. A worker that exits 0 having written nothing is a
    /// measured, recurring behaviour of this transport; an empty turn is something the
    /// loop already knows how to answer, where an exception threw away eleven minutes of
    /// verified work in run 27.
    /// </summary>
    public static ChatResponse EmptyTurn(WorkerRequest request) =>
        new(new ChatMessage(ChatRole.Assistant, []))
        {
            ModelId = request.Model,
            ResponseId = request.RequestId,
            FinishReason = ChatFinishReason.Stop,
        };

    private static ChatResponse BuildResponse(
        WorkerReply reply, IReadOnlyList<WorkerToolCall> calls, WorkerRequest request)
    {
        List<AIContent> contents = [];
        if (!string.IsNullOrWhiteSpace(reply.Text)) contents.Add(new TextContent(reply.Text));
        for (var i = 0; i < calls.Count; i++)
            contents.Add(Call(calls[i], calls[i].CallId ?? $"{request.RequestId}_call_{i + 1}"));

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, contents))
        {
            ModelId = request.Model,
            ResponseId = request.RequestId,
            FinishReason = calls.Count > 0 ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop,
        };
    }

    /// <summary>The call as the loop executes it; arguments that are not an object arrive empty.</summary>
    private static FunctionCallContent Call(WorkerToolCall call, string callId) =>
        new(callId, call.Name, call.Arguments.ValueKind == JsonValueKind.Object
            ? ToArguments(call.Arguments) : new Dictionary<string, object?>());

    // Values stay as JsonElement — the same shape the provider clients hand the
    // function-invoking layer, so argument binding behaves identically.
    private static Dictionary<string, object?> ToArguments(JsonElement arguments) =>
        arguments.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value);
}
