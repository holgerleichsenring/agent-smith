using System.Text.Json;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// Parses JSON plan responses from the LLM into Plan domain objects.
/// Migrated from Infrastructure during the M.E.AI refactor (p0119a).
/// p0128a adds an opt-in strict mode that validates against plan.schema.json
/// and populates the new typed fields (Scope, OpenQuestions, TestImpact,
/// ConsumerImpact, Status). Legacy <see cref="Parse"/> path is preserved.
/// </summary>
public static class PlanParser
{
    /// <summary>
    /// Strict mode: validates the JSON against plan.schema.json via the supplied
    /// validator, then parses into a typed Plan with all schema fields populated.
    /// Returns the validation failure unmodified when validation fails, so callers
    /// (run-result rendering, RetryCoordinator hand-off) get the JSON Pointer +
    /// rule-description detail without further wrapping.
    /// </summary>
    public static PlanParseResult ParseStrict(string output, PlanOutputValidator validator)
    {
        var validation = validator.Validate(output);
        if (!validation.IsValid) return new PlanParseResult(null, validation);

        var plan = ParseStructured(output);
        return new PlanParseResult(plan, validation);
    }

    private static Plan ParseStructured(string rawJson)
    {
        var cleaned = StripMarkdownCodeBlock(rawJson);
        using var doc = JsonDocument.Parse(cleaned);
        var root = doc.RootElement;

        var summary = root.GetProperty("summary").GetString() ?? "";
        var steps = root.GetProperty("steps").EnumerateArray()
            .Select(ParseStrictStep)
            .ToList();
        var scope = ParseScope(root.GetProperty("scope"));
        var openQuestions = root.GetProperty("open_questions").EnumerateArray()
            .Select(ParseOpenQuestion)
            .ToList();
        var status = ParseStatus(root.GetProperty("status").GetString() ?? "complete");

        return new Plan(summary, steps, rawJson)
        {
            Scope = scope,
            OpenQuestions = openQuestions,
            TestImpact = ReadOptionalString(root, "test_impact"),
            ConsumerImpact = ReadOptionalString(root, "consumer_impact"),
            Status = status
        };
    }

    private static PlanStep ParseStrictStep(JsonElement element)
    {
        var id = element.GetProperty("id").GetInt32();
        var action = element.GetProperty("action").GetString() ?? "";
        var file = element.TryGetProperty("file", out var f) ? f.GetString() : null;
        var changeType = element.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";
        var target = string.IsNullOrEmpty(file) ? null : new FilePath(file!);
        return new PlanStep(id, action, target, changeType);
    }

    private static PlanScope ParseScope(JsonElement element)
    {
        var files = element.GetProperty("files").EnumerateArray()
            .Select(e => e.GetString() ?? "").ToList();
        var modules = element.GetProperty("modules").EnumerateArray()
            .Select(e => e.GetString() ?? "").ToList();
        return new PlanScope(files, modules);
    }

    private static PlanOpenQuestion ParseOpenQuestion(JsonElement element)
    {
        var id = element.GetProperty("id").GetString() ?? "";
        var question = element.GetProperty("question").GetString() ?? "";
        var options = element.GetProperty("options").EnumerateArray()
            .Select(e => e.GetString() ?? "").ToList();
        return new PlanOpenQuestion(id, question, options);
    }

    private static PlanStatus ParseStatus(string raw) => raw switch
    {
        "needs_user_input" => PlanStatus.NeedsUserInput,
        _ => PlanStatus.Complete
    };

    private static string? ReadOptionalString(JsonElement root, string name)
        => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    public static Plan Parse(string providerName, string rawJson)
    {
        try
        {
            var cleaned = StripMarkdownCodeBlock(rawJson);
            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var summary = root.GetProperty("summary").GetString() ?? "";
            var steps = root.GetProperty("steps").EnumerateArray()
                .Select(ParseStep)
                .ToList();
            var decisions = root.TryGetProperty("decisions", out var dArr)
                ? dArr.EnumerateArray().Select(ParseDecision).ToList()
                : new List<PlanDecision>();

            return new Plan(summary, steps, rawJson, decisions);
        }
        catch (JsonException ex)
        {
            throw new ProviderException(
                providerName,
                $"Failed to parse plan response from {providerName}: {ex.Message}",
                ex);
        }
    }

    private static string StripMarkdownCodeBlock(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
                trimmed = trimmed[(firstNewline + 1)..];
        }
        if (trimmed.EndsWith("```"))
            trimmed = trimmed[..^3].TrimEnd();
        return trimmed;
    }

    private static PlanDecision ParseDecision(JsonElement element)
    {
        var category = element.TryGetProperty("category", out var cat)
            ? cat.GetString() ?? "Implementation"
            : "Implementation";
        var decision = element.GetProperty("decision").GetString() ?? "";
        return new PlanDecision(category, decision);
    }

    private static PlanStep ParseStep(JsonElement element)
    {
        var order = element.GetProperty("order").GetInt32();
        var description = element.GetProperty("description").GetString() ?? "";
        var targetFile = element.TryGetProperty("target_file", out var tf)
            ? new FilePath(tf.GetString()!)
            : null;
        var changeType = element.TryGetProperty("change_type", out var ct)
            ? ct.GetString() ?? "Modify"
            : "Modify";

        return new PlanStep(order, description, targetFile, changeType);
    }
}
