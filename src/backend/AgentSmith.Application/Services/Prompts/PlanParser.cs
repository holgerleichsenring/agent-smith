using System.Text.Json;
using System.Text.RegularExpressions;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using static AgentSmith.Application.Services.Prompts.PlanJsonElementMapper;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// Parses JSON plan responses from the LLM into Plan domain objects.
/// Migrated from Infrastructure during the M.E.AI refactor (p0119a).
/// p0128a adds an opt-in strict mode that validates against plan.schema.json
/// and populates the new typed fields (Scope, OpenQuestions, TestImpact,
/// ConsumerImpact, Status). Fence stripping and prose extraction flow through
/// <see cref="ITolerantJsonParser"/>. Per-element mapping lives in
/// <see cref="PlanJsonElementMapper"/> to keep this class within the size cap.
/// </summary>
public sealed class PlanParser(ITolerantJsonParser tolerantParser)
{
    public PlanParseResult ParseStrict(string output, PlanOutputValidator validator)
    {
        var validation = validator.Validate(output);
        if (!validation.IsValid) return new PlanParseResult(null, validation);
        return new PlanParseResult(ParseStructured(output), validation);
    }

    private Plan ParseStructured(string rawJson)
    {
        var parsed = tolerantParser.ParseObject(rawJson);
        if (parsed.Document is null)
            throw new JsonException("plan response did not contain a parseable JSON object");
        using var doc = parsed.Document;
        var root = doc.RootElement;

        var summary = root.GetProperty("summary").GetString() ?? "";
        var steps = root.GetProperty("steps").EnumerateArray().Select(MapStrictStep).ToList();
        var scope = MapScope(root.GetProperty("scope"));
        var openQuestions = root.GetProperty("open_questions").EnumerateArray()
            .Select(MapOpenQuestion).ToList();
        var status = MapStatus(root.GetProperty("status").GetString() ?? "complete");

        return new Plan(summary, steps, rawJson)
        {
            Scope = scope,
            OpenQuestions = openQuestions,
            TestImpact = ReadOptionalString(root, "test_impact"),
            ConsumerImpact = ReadOptionalString(root, "consumer_impact"),
            Status = status
        };
    }

    public Plan Parse(string providerName, string rawJson)
    {
        try
        {
            var parsed = tolerantParser.ParseObject(rawJson);
            if (parsed.Document is null)
                throw new JsonException("plan response did not contain a parseable JSON object");
            using var doc = parsed.Document;
            var root = doc.RootElement;
            // p0340: tolerant — a JSON object missing summary/steps yields an empty
            // (but present) plan rather than throwing; only a NON-object response
            // falls through to prose salvage in the handler.
            var summary = root.TryGetProperty("summary", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString() ?? "" : "";
            var steps = root.TryGetProperty("steps", out var st) && st.ValueKind == JsonValueKind.Array
                ? st.EnumerateArray().Select(MapLegacyStep).ToList() : new List<PlanStep>();
            var decisions = root.TryGetProperty("decisions", out var dArr)
                ? dArr.EnumerateArray().Select(MapDecision).ToList()
                : new List<PlanDecision>();
            return new Plan(summary, steps, rawJson, decisions);
        }
        catch (JsonException ex)
        {
            throw new ProviderException(providerName,
                $"Failed to parse plan response from {providerName}: {ex.Message}", ex);
        }
    }

    // p0340: last-resort salvage — the strict schema rejected the response (e.g. it lacked
    // scope/open_questions/status) and the tolerant JSON parse also failed (truncated at
    // MaxOutputTokens, or wrapped). p0376: this is the DISPLAY-vs-parse decoupling — the
    // plan.md must render readable content, never a raw JSON blob. Recover the summary +
    // steps from whatever is there (extractable JSON first, then a numbered/bulleted prose
    // list) and NEVER surface a raw JSON/markup blob as the summary. Never throws.
    public Plan SalvageProse(string rawText)
    {
        var text = (rawText ?? "").Trim();
        if (TryExtractJsonPlan(text, out var jsonPlan)) return jsonPlan;

        var steps = new List<PlanStep>();
        var order = 1;
        foreach (var line in text.Split('\n'))
        {
            var match = ProseStep.Match(line.Trim());
            if (match.Success && match.Groups["text"].Value.Trim().Length > 0)
                steps.Add(new PlanStep(order++, match.Groups["text"].Value.Trim(), null, "Modify"));
        }
        var summary = steps.Count > 0
            ? $"Plan salvaged from {steps.Count} prose step(s) — the planner did not return JSON"
            : SafeSummary(text);
        return new Plan(summary, steps, text);
    }

    // p0376: pull a plan out of a JSON response the strict path rejected. Tries a real
    // parse first (a valid {summary,steps} the schema refused for missing optional fields);
    // if the JSON is malformed/truncated so it will not parse, still recover the summary
    // string with a regex so the plan.md shows the intent, not the raw object.
    private bool TryExtractJsonPlan(string text, out Plan plan)
    {
        plan = null!;
        try
        {
            var parsed = tolerantParser.ParseObject(text);
            if (parsed.Document is not null)
            {
                using var doc = parsed.Document;
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("summary", out var s)
                    && s.ValueKind == JsonValueKind.String
                    && s.GetString() is { Length: > 0 } summary)
                {
                    var steps = root.TryGetProperty("steps", out var st) && st.ValueKind == JsonValueKind.Array
                        ? st.EnumerateArray().Select(MapLegacyStep).ToList()
                        : new List<PlanStep>();
                    plan = new Plan(summary, steps, text);
                    return true;
                }
            }
        }
        catch (JsonException) { /* fall through to the regex recovery below */ }

        // Malformed/truncated JSON: recover just the summary field so the display is readable.
        var m = SummaryField.Match(text);
        if (m.Success)
        {
            var recovered = Regex.Unescape(m.Groups["v"].Value).Trim();
            if (recovered.Length > 0)
            {
                plan = new Plan(recovered, new List<PlanStep>(), text);
                return true;
            }
        }
        return false;
    }

    // Never surface a raw JSON / markup blob as the plan summary.
    private static string SafeSummary(string text) =>
        text.StartsWith('{') || text.StartsWith('[')
            ? "The planner returned an unparseable response; the coding agent will plan from the ticket."
            : text;

    private static readonly Regex ProseStep =
        new(@"^(?:\d+[.)]|[-*•])\s+(?<text>.+)$", RegexOptions.Compiled);

    private static readonly Regex SummaryField =
        new(@"""summary""\s*:\s*""(?<v>(?:[^""\\]|\\.)*)""",
            RegexOptions.Compiled | RegexOptions.Singleline);
}
