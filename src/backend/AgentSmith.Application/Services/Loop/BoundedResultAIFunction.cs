using System.Text.Json;
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
/// p0423: it reports the size it cut FROM, because a bound whose effect nobody can see
/// is indistinguishable from a tool that returned little.
/// </para>
/// </summary>
public sealed class BoundedResultAIFunction(
    AIFunction inner, ResultBoundReporter? reporter = null, int budgetChars = 100_000)
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
        if (result is not string text) return result;
        reporter?.Report(text.Length);
        return Bound(text, budgetChars);
    }

    internal static string Bound(string text, int budgetChars)
    {
        if (text.Length <= budgetChars) return text;
        var half = budgetChars / 2;
        var dropped = text.Length - (half * 2);
        return text[..half]
            + $"\n\n… {dropped:N0} characters dropped from the middle of this result "
            + $"({text.Length:N0} in total). Ask for the part you need — narrow the path, "
            + "the pattern or the line range.\n\n"
            + text[^half..];
    }
}
