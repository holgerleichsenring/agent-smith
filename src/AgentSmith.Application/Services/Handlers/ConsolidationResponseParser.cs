using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Parses the JSON consolidation response from the LLM into structured findings and assessments.
/// Supports both array-based summary (preferred) and legacy string summary (backward compat).
/// </summary>
internal static class ConsolidationResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static ConsolidationParseResult Parse(string response)
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
                    var assessments = (parsed.Assessments ?? [])
                        .Where(a => a.Status is "confirmed" or "false_positive")
                        .Select(a => new FindingAssessment(
                            a.File ?? "", a.Line, a.Title ?? "", a.Status ?? "confirmed", a.Reason ?? ""))
                        .ToList();

                    var findings = BuildFindings(parsed);
                    var rawSummary = BuildRawSummary(parsed, findings);

                    return new ConsolidationParseResult(findings, assessments, rawSummary);
                }
            }
        }
        catch
        {
            // JSON parsing failed — fall back to raw text
        }

        var fallbackFindings = response.Split('\n')
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select((l, i) => new DiscussionFinding(i + 1, l.TrimStart('-', ' ', '*')))
            .ToList();

        return new ConsolidationParseResult(fallbackFindings, [], response);
    }

    private static List<DiscussionFinding> BuildFindings(ConsolidationResponse parsed)
    {
        // Prefer structured array if LLM returned it
        if (parsed.SummaryItems is { Count: > 0 })
        {
            return parsed.SummaryItems
                .Select((item, i) => new DiscussionFinding(
                    item.Order > 0 ? item.Order : i + 1,
                    item.Content ?? ""))
                .Where(f => !string.IsNullOrWhiteSpace(f.Content))
                .ToList();
        }

        // Fallback: split legacy string summary
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
        public List<AssessmentEntry>? Assessments { get; set; }
    }

    private sealed class SummaryItem
    {
        public int Order { get; set; }
        public string? Content { get; set; }
    }

    private sealed class AssessmentEntry
    {
        public string? File { get; set; }
        public int Line { get; set; }
        public string? Title { get; set; }
        public string? Status { get; set; }
        public string? Reason { get; set; }
    }
}

/// <summary>
/// Result of parsing a consolidation LLM response.
/// </summary>
internal sealed record ConsolidationParseResult(
    List<DiscussionFinding> Findings,
    List<FindingAssessment> Assessments,
    string RawSummary);
