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

    // p0340: last-resort salvage — the planner returned prose (a numbered / bulleted
    // list) instead of JSON. Turn each list item into a step so a plan is PRESENT at
    // the Approval / open-questions gate rather than silently empty (which disabled
    // the clarification gate). Never throws.
    public Plan SalvageProse(string rawText)
    {
        var steps = new List<PlanStep>();
        var order = 1;
        foreach (var line in (rawText ?? "").Split('\n'))
        {
            var match = ProseStep.Match(line.Trim());
            if (match.Success && match.Groups["text"].Value.Trim().Length > 0)
                steps.Add(new PlanStep(order++, match.Groups["text"].Value.Trim(), null, "Modify"));
        }
        var summary = steps.Count > 0
            ? $"Plan salvaged from {steps.Count} prose step(s) — the planner did not return JSON"
            : (rawText ?? "").Trim();
        return new Plan(summary, steps, rawText ?? "");
    }

    private static readonly Regex ProseStep =
        new(@"^(?:\d+[.)]|[-*•])\s+(?<text>.+)$", RegexOptions.Compiled);
}
