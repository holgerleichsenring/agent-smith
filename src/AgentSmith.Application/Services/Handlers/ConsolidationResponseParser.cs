using System.Text.Json;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Parses the JSON consolidation response from the LLM into a summary and finding assessments.
/// </summary>
internal static class ConsolidationResponseParser
{
    internal static (string Summary, List<FindingAssessment> Assessments) Parse(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response[jsonStart..(jsonEnd + 1)];
                var parsed = JsonSerializer.Deserialize<ConsolidationResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed is not null)
                {
                    var assessments = (parsed.Assessments ?? [])
                        .Where(a => a.Status is "confirmed" or "false_positive")
                        .Select(a => new FindingAssessment(
                            a.File ?? "", a.Line, a.Title ?? "", a.Status ?? "confirmed", a.Reason ?? ""))
                        .ToList();

                    return (parsed.Summary ?? response, assessments);
                }
            }
        }
        catch
        {
            // JSON parsing failed — fall back to raw text as summary
        }

        return (response, []);
    }

    private sealed class ConsolidationResponse
    {
        public string? Summary { get; set; }
        public List<AssessmentEntry>? Assessments { get; set; }
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
