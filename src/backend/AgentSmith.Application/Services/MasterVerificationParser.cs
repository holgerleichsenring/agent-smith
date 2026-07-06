using System.Text.Json;
using System.Text.RegularExpressions;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0241: extracts the coding master's structured verification verdict from its
/// final free-text answer. Tolerant on purpose — the model decides how to phrase
/// its message, so we accept a fenced ```verdict / ```json block OR a bare JSON
/// object anywhere in the text, snake_case or camelCase keys, and take the LAST
/// such block (the agent's final word). Returns null when no verdict object with
/// a recognisable "status" is present; the keystone treats that as "unverified".
/// </summary>
public static partial class MasterVerificationParser
{
    public static MasterVerification? TryParse(string? finalText)
    {
        if (string.IsNullOrWhiteSpace(finalText)) return null;
        foreach (var json in CandidateBlocks(finalText))
            if (TryReadObject(json, out var verification))
                return verification;
        return null;
    }

    // Fenced blocks first (most explicit), then any balanced-brace object — both
    // scanned last-to-first so the agent's final verdict wins over earlier drafts.
    private static IEnumerable<string> CandidateBlocks(string text)
    {
        var fenced = FenceRegex().Matches(text);
        for (var i = fenced.Count - 1; i >= 0; i--)
            yield return fenced[i].Groups["body"].Value;

        foreach (var obj in BalancedObjects(text))
            yield return obj;
    }

    private static IEnumerable<string> BalancedObjects(string text)
    {
        var results = new List<string>();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '{') continue;
            var depth = 0;
            for (var j = i; j < text.Length; j++)
            {
                if (text[j] == '{') depth++;
                else if (text[j] == '}')
                {
                    depth--;
                    if (depth == 0) { results.Add(text[i..(j + 1)]); i = j; break; }
                }
            }
        }
        results.Reverse();
        return results;
    }

    private static bool TryReadObject(string json, out MasterVerification verification)
    {
        verification = null!;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return false; }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            var status = MapStatus(GetString(doc.RootElement, "status", "verdict", "result"));
            if (status is null) return false;
            verification = new MasterVerification(
                status.Value,
                BuildRan: GetBool(doc.RootElement, "build_ran", "buildRan"),
                BuildPassed: GetBool(doc.RootElement, "build_passed", "buildPassed"),
                TestsRan: GetBool(doc.RootElement, "tests_ran", "testsRan"),
                TestsPassed: GetBool(doc.RootElement, "tests_passed", "testsPassed"),
                Summary: GetString(doc.RootElement, "summary", "notes"),
                // p0273: raw failing-test lists — the framework diffs them for regressions.
                FailingTests: GetStringArray(doc.RootElement, "failing_tests", "failingTests"),
                BaselineFailingTests: GetStringArray(
                    doc.RootElement, "baseline_failing_tests", "baselineFailingTests"),
                // p0316: ticket instructions the master refused to follow (quote + reason).
                IgnoredInstructions: GetIgnoredInstructions(
                    doc.RootElement, "ignored_instructions", "ignoredInstructions"));
            return true;
        }
    }

    private static VerificationStatus? MapStatus(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "green" or "pass" or "passed" or "ok" or "success" or "passing" => VerificationStatus.Green,
        "no-tests" or "no_tests" or "notests" or "none" or "no tests" => VerificationStatus.NoTests,
        "failed" or "fail" or "red" or "error" or "failing" => VerificationStatus.Failed,
        null or "" => null,
        _ => VerificationStatus.Unknown,
    };

    private static string? GetString(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
            if (TryGet(obj, name, out var el) && el.ValueKind == JsonValueKind.String)
                return el.GetString();
        return null;
    }

    // p0273: a JSON string array (e.g. failing_tests). Returns null when the key
    // is absent (skill didn't report it → keystone falls back to the binary gate);
    // an empty array returns an empty list (skill reported "nothing failing").
    private static IReadOnlyList<string>? GetStringArray(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
            if (TryGet(obj, name, out var el) && el.ValueKind == JsonValueKind.Array)
                return el.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .ToList();
        return null;
    }

    // p0316: an array of { quote, reason } objects. Absent = null (nothing ignored);
    // entries missing a quote are skipped. Tolerant of snake_case / camelCase keys.
    private static IReadOnlyList<IgnoredInstruction>? GetIgnoredInstructions(
        JsonElement obj, params string[] names)
    {
        foreach (var name in names)
            if (TryGet(obj, name, out var el) && el.ValueKind == JsonValueKind.Array)
            {
                var list = new List<IgnoredInstruction>();
                foreach (var item in el.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var quote = GetString(item, "quote", "instruction", "text");
                    if (string.IsNullOrWhiteSpace(quote)) continue;
                    list.Add(new IgnoredInstruction(
                        quote, GetString(item, "reason", "why") ?? ""));
                }
                return list;
            }
        return null;
    }

    private static bool GetBool(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
            if (TryGet(obj, name, out var el))
            {
                if (el.ValueKind == JsonValueKind.True) return true;
                if (el.ValueKind == JsonValueKind.False) return false;
                if (el.ValueKind == JsonValueKind.String && bool.TryParse(el.GetString(), out var b)) return b;
            }
        return false;
    }

    private static bool TryGet(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var prop in obj.EnumerateObject())
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        value = default;
        return false;
    }

    [GeneratedRegex(@"```[a-zA-Z]*\s*(?<body>\{[\s\S]*?\})\s*```", RegexOptions.Multiline)]
    private static partial Regex FenceRegex();
}
