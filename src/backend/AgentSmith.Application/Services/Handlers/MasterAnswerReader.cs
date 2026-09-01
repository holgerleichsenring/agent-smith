using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-01-6c32: reads a master's closing answer into observations, and says which of
/// the three things it was.
/// <para>
/// The findings merge used to decide array-ness with a strict parse of the extracted span
/// and only then call <see cref="ObservationParser"/> — whose resilient fallback exists
/// precisely to recover an array that hit the output cap mid-write. The gate was stricter
/// than the parser it guarded, so that recovery had never run on the security-scan path:
/// a truncated triage was rejected before the code that could read it was reached.
/// Array-ness is now decided by what the parser actually recovers.
/// </para>
/// </summary>
public sealed class MasterAnswerReader(
    ObservationParser observationParser,
    ITolerantJsonParser tolerantParser)
{
    /// <summary>
    /// Reads <paramref name="answer"/> as the master's observation array. A strictly valid
    /// array is taken at face value (an empty one is a triage that kept nothing); anything
    /// else is offered to the resilient recovery, and only an answer that yields no
    /// observation at all is rejected.
    /// </summary>
    public MasterAnswerReading Read(
        string answer, string role, ILogger? logger = null,
        IReadOnlyCollection<string>? readPaths = null)
    {
        if (IsStrictArray(answer))
            return new MasterAnswerReading(
                observationParser.TryParseWithoutIds(answer, role, logger, readPaths) ?? [],
                Recovered: false, Rejection: null);

        var recovered = observationParser.TryParseWithoutIds(answer, role, logger, readPaths);
        if (recovered is { Count: > 0 })
        {
            logger?.LogWarning(
                "Master '{Role}' answer is not a well-formed JSON array — recovered {Count} "
                + "observation(s) from it object by object", role, recovered.Count);
            return new MasterAnswerReading(recovered, Recovered: true, Rejection: null);
        }

        return new MasterAnswerReading(
            [], Recovered: false,
            Rejection: "answer is not a JSON array and holds no recoverable observation");
    }

    private bool IsStrictArray(string answer)
    {
        using var document = tolerantParser.ParseArray(answer).Document;
        return document is not null && document.RootElement.ValueKind == JsonValueKind.Array;
    }
}
