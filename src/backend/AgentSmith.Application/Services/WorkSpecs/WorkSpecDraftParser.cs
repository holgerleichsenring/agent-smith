using System.Text.Json;
using AgentSmith.Contracts.WorkSpecs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: extracts the derivation model's JSON object from its reply — tolerant
/// like ExpectationDraftParser (fenced block or bare object anywhere in the
/// text; first object carrying a "goal" wins). The revision header is NOT read
/// from the model: the handler owns numbering and cause, so the model cannot
/// rewrite its own history.
/// </summary>
public static class WorkSpecDraftParser
{
    public static WorkSpecDraft? TryParse(string? text, string key)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (var json in WorkSpecJsonReader.BalancedObjects(text))
            if (TryReadObject(json, key, out var draft))
                return draft;
        return null;
    }

    private static bool TryReadObject(string json, string key, out WorkSpecDraft draft)
    {
        draft = null!;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return false; }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            if (!WorkSpecJsonReader.TryGet(root, "goal", out var goal)
                || goal.ValueKind != JsonValueKind.String)
                return false;
            draft = Build(root, key);
            return true;
        }
    }

    private static WorkSpecDraft Build(JsonElement root, string key) => new(
        new WorkSpecArtifact(
            new WorkSpec(
                key,
                WorkSpecJsonReader.ReadString(root, "goal"),
                WorkSpecJsonReader.ReadStrings(root, "requirements"),
                ReadConstraints(root),
                WorkSpecJsonReader.ReadStrings(root, "done"),
                DoneIsReadOnly: false,
                WorkSpecJsonReader.ReadStrings(root, "assumptions"),
                Revisions: [],
                ReadHandback(root)),
            WorkSpecJsonReader.ReadString(root, "samplesmarkdown")),
        ReadIgnoredInstructions(root));

    private static IReadOnlyList<WorkSpecConstraint> ReadConstraints(JsonElement root) =>
        [.. WorkSpecJsonReader.ReadObjects(root, "constraints")
            .Select(e => new WorkSpecConstraint(
                WorkSpecJsonReader.ReadString(e, "rule"),
                WorkSpecJsonReader.ReadString(e, "sampleanchor") is { Length: > 0 } a ? a : null))
            .Where(c => c.Rule.Length > 0)];

    private static IReadOnlyList<IgnoredInstruction> ReadIgnoredInstructions(JsonElement root) =>
        [.. WorkSpecJsonReader.ReadObjects(root, "ignoredinstructions")
            .Select(e => new IgnoredInstruction(
                WorkSpecJsonReader.ReadString(e, "quote"),
                WorkSpecJsonReader.ReadString(e, "reason")))
            .Where(i => i.Quote.Length > 0)];

    private static WorkSpecHandback? ReadHandback(JsonElement root)
    {
        if (!WorkSpecJsonReader.TryGet(root, "handback", out var el)
            || el.ValueKind != JsonValueKind.Object)
            return null;
        var raw = WorkSpecJsonReader.ReadString(el, "case").Replace("_", string.Empty);
        if (!Enum.TryParse<WorkSpecHandbackCase>(raw, ignoreCase: true, out var parsed)
            || parsed == WorkSpecHandbackCase.None)
            return null;
        return new WorkSpecHandback(parsed, WorkSpecJsonReader.ReadString(el, "reason"));
    }
}
