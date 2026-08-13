using AgentSmith.Contracts.Models.Workers;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: renders the conversation a provider would have received into the worker
/// protocol — every message, every part, in order, including prior tool calls and their
/// results. A part this mapper cannot express is emitted as <c>unsupported</c> WITH its
/// CLR type, so the gap is declared in the payload instead of hidden by omission.
/// </summary>
public sealed class WorkerMessageMapper(WorkerJsonFormat json)
{
    public IReadOnlyList<WorkerMessage> Map(IEnumerable<ChatMessage> messages) =>
        [.. messages.Select(m => new WorkerMessage(m.Role.Value, [.. m.Contents.Select(MapPart)]))];

    private WorkerContentPart MapPart(AIContent content) => content switch
    {
        TextContent text => new WorkerContentPart("text", Text: text.Text),
        TextReasoningContent reasoning => new WorkerContentPart("reasoning", Text: reasoning.Text),
        FunctionCallContent call => new WorkerContentPart(
            "tool_call", CallId: call.CallId, Name: call.Name,
            Arguments: json.ToElement(call.Arguments)),
        FunctionResultContent result => new WorkerContentPart(
            "tool_result", CallId: result.CallId, Result: result.Result?.ToString() ?? string.Empty),
        DataContent data => new WorkerContentPart(
            "unsupported", Text: $"{data.MediaType} attachment, not rendered into the prompt",
            ClrType: nameof(DataContent)),
        _ => new WorkerContentPart("unsupported", ClrType: content.GetType().Name),
    };
}
