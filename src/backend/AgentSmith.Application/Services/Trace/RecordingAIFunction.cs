using System.Text.Json;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Trace;

/// <summary>
/// p0423: records a tool call's arguments and its result AS THE MODEL RECEIVED IT.
/// <para>
/// It is the OUTERMOST tool wrapper for exactly that reason. A result is bounded and
/// framed on its way up, and recording what the tool PRODUCED would document a reality the
/// agent never experienced — the confusion that made a p0422 fix unverifiable.
/// </para>
/// </summary>
public sealed class RecordingAIFunction(
    AIFunction inner, IRunTraceWriter trace, IRunContextAccessor runContext) : AIFunction
{
    public override string Name => inner.Name;
    public override string Description => inner.Description;
    public override JsonElement JsonSchema => inner.JsonSchema;
    public override JsonSerializerOptions JsonSerializerOptions => inner.JsonSerializerOptions;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var runId = runContext.CurrentRunId ?? string.Empty;
        var result = await inner.InvokeAsync(arguments, cancellationToken);
        await trace.WriteAsync(runId, "tool", Render(arguments, result), cancellationToken);
        return result;
    }

    private string Render(AIFunctionArguments arguments, object? result) =>
        $"=== call {inner.Name} ===\n{Serialize(arguments)}\n\n=== result as received ===\n{result}";

    private string Serialize(AIFunctionArguments arguments)
    {
        try { return JsonSerializer.Serialize(arguments, JsonSerializerOptions); }
        catch { return "{}"; }
    }
}
