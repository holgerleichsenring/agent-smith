using System.Text.Json;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Parses the JSON consolidation response from the LLM into structured findings.
/// Supports both array-based summary (preferred) and legacy string summary (backward compat).
/// On JSON parse failure the raw text is used as a degraded fallback — the failure is logged
/// so diagnosis is possible, and the fallback is explicit rather than silent.
/// p0123: Assessments path retired — review status now lives on SkillObservation.ReviewStatus,
/// applied via FilterRound rather than via a separate consolidation-emitted assessment list.
/// </summary>
internal static class ConsolidationResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static ConsolidationParseResult Parse(string response, ILogger? logger = null)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response[jsonStart..(jsonEnd + 1)];
                var parsed = JsonSerializer.Deserialize<ConsolidationResponse>(json, JsonOptions);

                if (parsed is not null)
                {
                    var findings = BuildFindings(parsed);
                    var rawSummary = BuildRawSummary(parsed, findings);

                    return new ConsolidationParseResult(findings, rawSummary);
                }

                logger?.LogWarning(
                    "Consolidation response parsed as null JSON object — falling back to raw text");
            }
            else
            {
                logger?.LogWarning(
                    "Consolidation response contained no JSON object (no matching braces) — falling back to raw text");
            }
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex,
                "Failed to parse consolidation JSON — falling back to raw text. Response length: {Length}",
                response.Length);
        }

        var fallbackFindings = response.Split('\n')
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select((l, i) => new DiscussionFinding(i + 1, l.TrimStart('-', ' ', '*')))
            .ToList();

        return new ConsolidationParseResult(fallbackFindings, response);
    }

    private static List<DiscussionFinding> BuildFindings(ConsolidationResponse parsed)
    {
        if (parsed.SummaryItems is { Count: > 0 })
        {
            return parsed.SummaryItems
                .Select((item, i) => new DiscussionFinding(
                    item.Order > 0 ? item.Order : i + 1,
                    item.Content ?? ""))
                .Where(f => !string.IsNullOrWhiteSpace(f.Content))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(parsed.Summary))
        {
            return parsed.Summary.Split('\n')
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select((l, i) => new DiscussionFinding(i + 1, l.TrimStart('-', ' ', '*', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.')))
                .Where(f => !string.IsNullOrWhiteSpace(f.Content))
                .ToList();
        }

        return [];
    }

    private static string BuildRawSummary(ConsolidationResponse parsed, List<DiscussionFinding> findings)
    {
        if (!string.IsNullOrWhiteSpace(parsed.Summary))
            return parsed.Summary;

        return string.Join("\n", findings.Select(f => $"{f.Order}. {f.Content}"));
    }

    private sealed class ConsolidationResponse
    {
        public string? Summary { get; set; }
        public List<SummaryItem>? SummaryItems { get; set; }
    }

    private sealed class SummaryItem
    {
        public int Order { get; set; }
        public string? Content { get; set; }
    }
}

/// <summary>
/// Result of parsing a consolidation LLM response.
/// </summary>
internal sealed record ConsolidationParseResult(
    List<DiscussionFinding> Findings,
    string RawSummary);
