using System.Text.Json;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: reads the refutations out of whatever the model wrote around them.
/// <para>
/// Tolerant about framing, strict about substance: an answer carrying no readable rows
/// is NOT "everything is substantiated" and not "everything is refuted" — it is a failed
/// call, and the caller must be able to tell that apart from a real verdict.
/// </para>
/// <para>
/// 2026-09-01-85b2: one call carries every candidate, so at thirty findings the ANSWER is
/// what runs out of room. A strict deserialize of a truncated array threw, the whole step
/// no-opped, and nothing said so. The complete object literals in a cut-off array are real
/// verdicts and are kept; the partial last one is not, and is dropped.
/// </para>
/// </summary>
public sealed class FindingRefutationReader(
    ITolerantJsonParser parser, ILogger<FindingRefutationReader> logger) : IFindingRefutationReader
{
    // snake_case is what the prompt asks for, and case-insensitivity alone
    // does not bridge an underscore.
    private static readonly JsonSerializerOptions Shape = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public IReadOnlyList<FindingRefutation>? Read(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : Whole(text) ?? Salvaged(text);

    private IReadOnlyList<FindingRefutation>? Whole(string text)
    {
        var start = text.IndexOf('[', StringComparison.Ordinal);
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start) return null;
        try
        {
            var rows = JsonSerializer.Deserialize<List<FindingRefutation>>(text[start..(end + 1)], Shape);
            return rows is null ? null : [.. rows.OfType<FindingRefutation>()];
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "The refutation answer is not a whole array — salvaging its rows");
            return null;
        }
    }

    private IReadOnlyList<FindingRefutation>? Salvaged(string text)
    {
        var rows = new List<FindingRefutation>();
        foreach (var literal in parser.ExtractArrayObjects(text))
        {
            try
            {
                var row = JsonSerializer.Deserialize<FindingRefutation>(literal, Shape);
                if (row is not null) rows.Add(row);
            }
            catch (JsonException ex)
            {
                logger.LogDebug(ex, "Discarding an unreadable refutation row");
            }
        }
        if (rows.Count > 0)
            logger.LogWarning(
                "The refutation answer was cut short — {Rows} complete verdict(s) salvaged, "
                + "every finding without one stands", rows.Count);
        return rows.Count == 0 ? null : rows;
    }
}
