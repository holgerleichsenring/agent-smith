using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-25-e94ec: wraps one of the series master's acts — a ticket act, a pause, a question, the series check — so
/// the beat knows the master did something a person reads about. The beat posts the master's reply only then, when
/// its own question was answered, or when the operator wrote; a beat in which it only read says nothing. An attempt
/// counts, refused or not: "I could not retry it" is worth the operator's reading.
/// </summary>
public sealed class SeriesActedFunction(AIFunction inner, SeriesBeat beat) : AIFunction
{
    public override string Name => inner.Name;
    public override string Description => inner.Description;
    public override JsonElement JsonSchema => inner.JsonSchema;
    public override JsonSerializerOptions JsonSerializerOptions => inner.JsonSerializerOptions;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        beat.MasterActed = true;
        return inner.InvokeAsync(arguments, cancellationToken);
    }
}
