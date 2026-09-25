using System.Text.Json.Nodes;
using AgentSmith.Application.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// 2026-09-25-e5b1: rewrites the retired coding-preset names out of ONE stored config document.
/// Pure — it is handed a document and answers with what it should become, or null when the
/// document names none of them, which is what keeps an untouched document from being re-saved.
/// <para>
/// The fields are NAMED rather than searched for: a blind string replace over a config document
/// would also rewrite a repository, a label or a secret id that happens to read <c>fix-bug</c>.
/// Three document types carry a pipeline name, and every field of each is here —
/// <c>pipeline_trigger</c>, whose whole document IS the name (the label is its id);
/// <c>tracker</c>, in its label map's VALUES and its default; and <c>project</c>, in
/// <c>pipeline</c>, every <c>pipelines[].name</c>, <c>default_pipeline</c>, and each of the four
/// per-platform trigger blocks' own label map and default.
/// </para>
/// <para>
/// All of them or none of them, per document: a default rewritten while the pipelines list it
/// must appear in keeps the old spelling names a pipeline the project does not declare, and a
/// project whose default is not in its own list does not run at all.
/// </para>
/// </summary>
internal static class PipelineAliasRewrite
{
    private static readonly string[] TriggerBlocks =
        ["jiraTrigger", "githubTrigger", "gitlabTrigger", "azuredevopsTrigger"];

    /// <summary>The rewritten document, or null when this type carries no pipeline name or this
    /// document names no retired one.</summary>
    public static string? Apply(string type, string doc)
    {
        if (type == ConfigDocTypes.PipelineTrigger) return WholeDocument(doc);
        if (type != ConfigDocTypes.Tracker && type != ConfigDocTypes.Project) return null;
        if (JsonNode.Parse(doc) is not JsonObject root) return null;
        var changed = type == ConfigDocTypes.Tracker ? Routing(root) : Project(root);
        return changed ? root.ToJsonString() : null;
    }

    // The global pipeline_triggers map is stored one row per LABEL, so the document is the bare
    // pipeline name — there is no field to name.
    private static string? WholeDocument(string doc) =>
        JsonNode.Parse(doc) is JsonValue value && value.TryGetValue<string>(out var name)
        && RetiredPipelineNames.ReplacementFor(name) is { } target
            ? JsonValue.Create(target).ToJsonString()
            : null;

    /// <summary>A trigger-shaped object — a tracker or one of a project's platform blocks.</summary>
    private static bool Routing(JsonObject o) =>
        MapValues(o, "pipelineFromLabel") | Name(o, "defaultPipeline");

    private static bool Project(JsonObject o)
    {
        var changed = Name(o, "pipeline") | Name(o, "defaultPipeline") | Declared(o);
        foreach (var block in TriggerBlocks)
            if (Find(o, block) is JsonObject trigger) changed |= Routing(trigger);
        return changed;
    }

    /// <summary>project.pipelines[]: the name of each declared pipeline definition.</summary>
    private static bool Declared(JsonObject o)
    {
        if (Find(o, "pipelines") is not JsonArray list) return false;
        var changed = false;
        foreach (var entry in list)
            if (entry is JsonObject definition) changed |= Name(definition, "name");
        return changed;
    }

    /// <summary>A label map: the keys are the operator's labels, the VALUES are ours.</summary>
    private static bool MapValues(JsonObject o, string field)
    {
        if (Find(o, field) is not JsonObject map) return false;
        var changed = false;
        foreach (var label in map.Select(pair => pair.Key).ToList())
            changed |= Name(map, label);
        return changed;
    }

    private static bool Name(JsonObject o, string field)
    {
        if (Key(o, field) is not { } key) return false;
        if (o[key] is not JsonValue value || !value.TryGetValue<string>(out var name)) return false;
        if (RetiredPipelineNames.ReplacementFor(name) is not { } target) return false;
        o[key] = JsonValue.Create(target);
        return true;
    }

    private static JsonNode? Find(JsonObject o, string field) => Key(o, field) is { } key ? o[key] : null;

    // The store serializes PascalCase and deserializes case-insensitively, and an imported or
    // hand-edited document may carry either spelling — so a field is found the way the store
    // finds it, never by one exact casing the document is not obliged to use.
    private static string? Key(JsonObject o, string field) =>
        o.Select(pair => pair.Key)
            .FirstOrDefault(key => string.Equals(key, field, StringComparison.OrdinalIgnoreCase));
}
