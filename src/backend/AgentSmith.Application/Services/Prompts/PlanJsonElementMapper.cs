using System.Text.Json;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// Per-element mapping helpers split out of <see cref="PlanParser"/> to keep
/// both files under the 120-line class cap. Pure JSON-element → domain mapping;
/// no I/O, no logging.
/// </summary>
internal static class PlanJsonElementMapper
{
    internal static PlanStep MapStrictStep(JsonElement element)
    {
        var id = element.GetProperty("id").GetInt32();
        var action = element.GetProperty("action").GetString() ?? "";
        var file = element.TryGetProperty("file", out var f) ? f.GetString() : null;
        var changeType = element.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";
        var target = string.IsNullOrEmpty(file) ? null : new FilePath(file!);
        return new PlanStep(id, action, target, changeType);
    }

    internal static PlanScope MapScope(JsonElement element)
    {
        var files = element.GetProperty("files").EnumerateArray().Select(e => e.GetString() ?? "").ToList();
        var modules = element.GetProperty("modules").EnumerateArray().Select(e => e.GetString() ?? "").ToList();
        return new PlanScope(files, modules);
    }

    internal static PlanOpenQuestion MapOpenQuestion(JsonElement element)
    {
        var id = element.GetProperty("id").GetString() ?? "";
        var question = element.GetProperty("question").GetString() ?? "";
        var options = element.GetProperty("options").EnumerateArray().Select(e => e.GetString() ?? "").ToList();
        return new PlanOpenQuestion(id, question, options);
    }

    internal static PlanStatus MapStatus(string raw) => raw switch
    {
        "needs_user_input" => PlanStatus.NeedsUserInput,
        _ => PlanStatus.Complete
    };

    internal static string? ReadOptionalString(JsonElement root, string name)
        => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    internal static PlanDecision MapDecision(JsonElement element)
    {
        var category = element.TryGetProperty("category", out var cat)
            ? cat.GetString() ?? "Implementation" : "Implementation";
        var decision = element.GetProperty("decision").GetString() ?? "";
        return new PlanDecision(category, decision);
    }

    internal static PlanStep MapLegacyStep(JsonElement element)
    {
        var order = element.GetProperty("order").GetInt32();
        var description = element.GetProperty("description").GetString() ?? "";
        var targetFile = element.TryGetProperty("target_file", out var tf)
            ? new FilePath(tf.GetString()!) : null;
        var changeType = element.TryGetProperty("change_type", out var ct)
            ? ct.GetString() ?? "Modify" : "Modify";
        return new PlanStep(order, description, targetFile, changeType);
    }
}
