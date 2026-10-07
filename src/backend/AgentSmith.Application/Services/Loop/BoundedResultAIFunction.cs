using System.Text.Json;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0422: bounds what ONE tool result may add to the conversation.
/// <para>
/// Run 20 died at "Prompt is too long" on a 4,135,613-character prompt, three calls after
/// a 152k one — not growth, an impact: a single tool returned megabytes and the failure
/// surfaced at the NEXT model call, far from its cause. Every tool bounded itself, or
/// did not; there was no place that bounded them all.
/// </para>
/// <para>
/// The cut keeps the HEAD and the TAIL. A listing says what it is at the start, a build
/// log says how it went at the end, and the middle of four megabytes is what nobody
/// needed. It says how much it dropped, so the model can ask for the rest deliberately
/// rather than wonder.
/// </para>
/// <para>
/// 2026-10-07-6b9da: the cut itself is <see cref="ToolResultBound"/>, the same one every tool
/// loop applies, so a result bounded here passes the loop's bound unchanged.
/// </para>
/// <para>
/// p0423: it reports the size it cut FROM, because a bound whose effect nobody can see
/// is indistinguishable from a tool that returned little.
/// </para>
/// </summary>
public sealed class BoundedResultAIFunction(
    AIFunction inner, ResultBoundReporter? reporter = null, int budgetChars = ToolResultBound.DefaultBudget)
    : AIFunction
{
    public override string Name => inner.Name;

    public override string Description => inner.Description;

    public override JsonElement JsonSchema => inner.JsonSchema;

    public override JsonSerializerOptions JsonSerializerOptions => inner.JsonSerializerOptions;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var result = await inner.InvokeAsync(arguments, cancellationToken);
        // AIFunctionFactory marshals a string return as a JSON string; both are bounded.
        var text = ToolResultBound.TextOf(result);
        if (text is null) return result;
        reporter?.Report(text.Length);
        return ToolResultBound.Apply(text, budgetChars);
    }
}
