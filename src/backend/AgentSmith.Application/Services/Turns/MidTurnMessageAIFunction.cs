using System.Globalization;
using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Turns;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Turns;

/// <summary>
/// 2026-11-12-a1fa: wraps a design-turn tool so the operator's messages sent while the turn runs reach the master with
/// the next tool result, as Claude Code hands them on: after the call, each waiting message of the turn's poster is
/// appended to its result under a heading of its own. A call with nothing waiting returns its result untouched.
/// <para>
/// The result stays one string. Every layer between here and the provider — secret masking, the forwarded-view fit,
/// compaction and the thread codec — reads a tool result as text, and a result made of parts would pass all of them
/// unread.
/// </para>
/// </summary>
public sealed class MidTurnMessageAIFunction(AIFunction inner, IMidTurnMessages messages) : AIFunction
{
    public const string Heading = "The operator wrote while you were working";

    public override string Name => inner.Name;
    public override string Description => inner.Description;
    public override JsonElement JsonSchema => inner.JsonSchema;
    public override JsonSerializerOptions JsonSerializerOptions => inner.JsonSerializerOptions;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var result = await inner.InvokeAsync(arguments, cancellationToken);
        var waiting = messages.Drain();
        return waiting.Count == 0 ? result : Appended(TextOf(result), waiting);
    }

    private static string Appended(string result, IReadOnlyList<MidTurnMessage> waiting)
    {
        var text = new StringBuilder(result);
        foreach (var message in waiting)
            text.Append("\n\n").Append(Heading).Append(" (")
                .Append(message.At.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture)).Append("):\n")
                .Append(message.Text);
        return text.ToString();
    }

    private string TextOf(object? result) => result switch
    {
        null => string.Empty,
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } json => json.GetString() ?? string.Empty,
        _ => JsonSerializer.Serialize(result, JsonSerializerOptions),
    };
}
