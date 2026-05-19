using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Parses the JSON consolidation response from the LLM into structured findings.
/// Supports both array-based summary (preferred) and legacy string summary.
/// On parse failure the raw text is used as a degraded fallback — the failure
/// is logged so diagnosis is possible. Fence stripping and prose extraction
/// flow through <see cref="ITolerantJsonParser"/>.
/// p0123: Assessments path retired — review status now lives on
/// SkillObservation.ReviewStatus, applied via FilterRound rather than via a
/// separate consolidation-emitted assessment list.
/// </summary>
public sealed class ConsolidationResponseParser(ITolerantJsonParser tolerantParser)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ConsolidationParseResult Parse(string response, ILogger? logger = null)
    {
        var parsed = tolerantParser.ParseObject(response);
        if (parsed.Document is null)
        {
            logger?.LogWarning(
                "Consolidation response had no parseable JSON object — falling back to raw text. Length: {Length}",
                response.Length);
            return FallbackToRawText(response);
        }
        using (parsed.Document)
        {
            try
            {
                var raw = parsed.Document.RootElement.Deserialize<ConsolidationResponse>(JsonOptions);
                if (raw is not null)
                {
                    var findings = BuildFindings(raw);
                    var rawSummary = BuildRawSummary(raw, findings);
                    return new ConsolidationParseResult(findings, rawSummary);
                }
                logger?.LogWarning("Consolidation response parsed as null — falling back to raw text");
            }
            catch (JsonException ex)
            {
                logger?.LogWarning(ex,
                    "Failed to map consolidation JSON to DTO — falling back to raw text. Length: {Length}",
                    response.Length);
            }
        }
        return FallbackToRawText(response);
    }

    private static ConsolidationParseResult FallbackToRawText(string response)
    {
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
                    item.Order > 0 ? item.Order : i + 1, item.Content ?? ""))
                .Where(f => !string.IsNullOrWhiteSpace(f.Content))
                .ToList();
        }
        if (!string.IsNullOrWhiteSpace(parsed.Summary))
        {
            return parsed.Summary.Split('\n')
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select((l, i) => new DiscussionFinding(
                    i + 1, l.TrimStart('-', ' ', '*', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.')))
                .Where(f => !string.IsNullOrWhiteSpace(f.Content))
                .ToList();
        }
        return [];
    }

    private static string BuildRawSummary(ConsolidationResponse parsed, List<DiscussionFinding> findings)
    {
        if (!string.IsNullOrWhiteSpace(parsed.Summary)) return parsed.Summary;
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

/// <summary>Result of parsing a consolidation LLM response.</summary>
public sealed record ConsolidationParseResult(
    List<DiscussionFinding> Findings,
    string RawSummary);
