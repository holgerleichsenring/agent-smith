using System.Text.Json;
using AgentSmith.Contracts.Models.Workers;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: turns a worker's reply back into the <see cref="ChatResponse"/> the agent loop
/// consumes unchanged — assistant text and tool calls the FunctionInvokingChatClient then
/// executes for real. Validation is strict: an empty answer, a call naming a tool that was
/// never offered, or arguments that are not an object is a failure with a reason, because
/// each one means the worker did not answer the call it was given.
/// </summary>
public sealed class WorkerReplyTranslator
{
    public bool TryTranslate(
        WorkerReply reply, WorkerRequest request, out ChatResponse response, out string? problem)
    {
        var calls = reply.ToolCalls ?? [];
        problem = Validate(reply, calls, request);
        response = problem is null ? BuildResponse(reply, calls, request) : new ChatResponse();
        return problem is null;
    }

    private static string? Validate(
        WorkerReply reply, IReadOnlyList<WorkerToolCall> calls, WorkerRequest request)
    {
        if (!string.IsNullOrWhiteSpace(reply.Error))
            return $"the worker refused the call: {reply.Error}";
        if (Unoffered(calls, request) is { } unknown)
            return $"the worker called tool '{unknown}', which was not offered. "
                + $"Offered: [{string.Join(", ", request.Tools.Select(t => t.Name))}]";
        if (calls.FirstOrDefault(c => c.Arguments.ValueKind != JsonValueKind.Object) is { } malformed)
            return $"the worker called tool '{malformed.Name}' with arguments that are not a "
                + $"JSON object (was {malformed.Arguments.ValueKind})";
        if (string.IsNullOrWhiteSpace(reply.Text) && calls.Count == 0)
            return "the worker answered with neither text nor tool calls";
        return null;
    }

    private static ChatResponse BuildResponse(
        WorkerReply reply, IReadOnlyList<WorkerToolCall> calls, WorkerRequest request)
    {
        List<AIContent> contents = [];
        if (!string.IsNullOrWhiteSpace(reply.Text)) contents.Add(new TextContent(reply.Text));
        for (var i = 0; i < calls.Count; i++)
            contents.Add(new FunctionCallContent(
                calls[i].CallId ?? $"{request.RequestId}_call_{i + 1}",
                calls[i].Name,
                ToArguments(calls[i].Arguments)));

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, contents))
        {
            ModelId = request.Model,
            ResponseId = request.RequestId,
            FinishReason = calls.Count > 0 ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop,
        };
    }

    private static string? Unoffered(IReadOnlyList<WorkerToolCall> calls, WorkerRequest request)
    {
        var offered = request.Tools.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        return calls.Select(c => c.Name).FirstOrDefault(name => !offered.Contains(name));
    }

    // Values stay as JsonElement — the same shape the provider clients hand the
    // function-invoking layer, so argument binding behaves identically.
    private static Dictionary<string, object?> ToArguments(JsonElement arguments) =>
        arguments.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value);
}
