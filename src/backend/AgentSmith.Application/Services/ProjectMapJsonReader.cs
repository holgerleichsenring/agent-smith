using System.Text.Json;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Default <see cref="IProjectMapJsonReader"/>: strips optional fences, parses
/// leniently (trailing commas, comments), and projects each top-level field
/// into the typed <see cref="ProjectMap"/>. Returns a friendly error string on
/// failure so the orchestrator can re-prompt the LLM with concrete diagnostics.
/// </summary>
public sealed class ProjectMapJsonReader : IProjectMapJsonReader
{
    private static readonly JsonDocumentOptions LenientJsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public bool TryRead(string finalText, out ProjectMap? map, out string error)
    {
        map = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(finalText))
        {
            error = "model returned empty response";
            return false;
        }

        var json = StripFences(finalText.Trim());
        try
        {
            using var doc = JsonDocument.Parse(json, LenientJsonOptions);
            map = Build(doc.RootElement);
            return true;
        }
        catch (JsonException ex) { error = ex.Message; return false; }
        catch (KeyNotFoundException ex) { error = $"missing required field: {ex.Message}"; return false; }
    }

    private static string StripFences(string json)
    {
        if (!json.StartsWith("```")) return json;
        var firstNl = json.IndexOf('\n');
        if (firstNl > 0) json = json[(firstNl + 1)..];
        if (json.EndsWith("```")) json = json[..^3].TrimEnd();
        return json;
    }

    private static ProjectMap Build(JsonElement root) => new(
        PrimaryLanguage: root.TryGetProperty("primary_language", out var lang) ? lang.GetString() ?? "unknown" : "unknown",
        Frameworks: ReadStringArray(root, "frameworks"),
        Modules: ReadModules(root),
        TestProjects: ReadTestProjects(root),
        EntryPoints: ReadStringArray(root, "entry_points"),
        Conventions: ReadConventions(root),
        Ci: ReadCi(root));

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string name) =>
        !root.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array
            ? []
            : arr.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString() ?? string.Empty)
                .Where(s => s.Length > 0)
                .ToList();

    private static IReadOnlyList<Module> ReadModules(JsonElement root) =>
        !root.TryGetProperty("modules", out var arr) || arr.ValueKind != JsonValueKind.Array
            ? []
            : arr.EnumerateArray().Select(e => new Module(
                Path: e.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "",
                Role: ParseRole(e.TryGetProperty("role", out var r) ? r.GetString() : null),
                DependsOn: ReadStringArray(e, "depends_on"))).ToList();

    private static IReadOnlyList<TestProject> ReadTestProjects(JsonElement root) =>
        !root.TryGetProperty("test_projects", out var arr) || arr.ValueKind != JsonValueKind.Array
            ? []
            : arr.EnumerateArray().Select(e => new TestProject(
                Path: e.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "",
                Framework: e.TryGetProperty("framework", out var f) ? f.GetString() ?? "" : "",
                FileCount: e.TryGetProperty("file_count", out var c) && c.TryGetInt32(out var i) ? i : 0,
                SampleTestPath: e.TryGetProperty("sample_test_path", out var s) ? s.GetString() : null)).ToList();

    private static Conventions ReadConventions(JsonElement root) =>
        !root.TryGetProperty("conventions", out var c) || c.ValueKind != JsonValueKind.Object
            ? new Conventions(null, null, null)
            : new Conventions(
                NamingPattern: c.TryGetProperty("naming_pattern", out var n) ? n.GetString() : null,
                TestLayout: c.TryGetProperty("test_layout", out var tl) ? tl.GetString() : null,
                ErrorHandling: c.TryGetProperty("error_handling", out var eh) ? eh.GetString() : null);

    private static CiConfig ReadCi(JsonElement root) =>
        !root.TryGetProperty("ci", out var ci) || ci.ValueKind != JsonValueKind.Object
            ? new CiConfig(false, null, null, null)
            : new CiConfig(
                HasCi: ci.TryGetProperty("has_ci", out var h) && h.ValueKind is JsonValueKind.True,
                BuildCommand: ci.TryGetProperty("build_command", out var b) ? b.GetString() : null,
                TestCommand: ci.TryGetProperty("test_command", out var t) ? t.GetString() : null,
                CiSystem: ci.TryGetProperty("ci_system", out var s) ? s.GetString() : null,
                InstallCommand: ci.TryGetProperty("install_command", out var ins) ? ins.GetString() : null);

    private static ModuleRole ParseRole(string? raw) => raw?.ToLowerInvariant() switch
    {
        "production" => ModuleRole.Production,
        "test" => ModuleRole.Test,
        "tool" => ModuleRole.Tool,
        "generated" => ModuleRole.Generated,
        _ => ModuleRole.Other
    };
}
