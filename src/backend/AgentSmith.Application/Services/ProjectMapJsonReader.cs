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

        // Strict first (clean or fenced JSON); then tolerate a prose-wrapped object by
        // scanning for the first balanced {...} that builds (e.g. a Sonnet 4.6 preamble).
        if (TryBuild(Json.FencedJson.Strip(finalText.Trim()), out map, out error))
            return true;
        foreach (var candidate in TolerantJsonObjectScanner.ExtractObjects(finalText))
            if (TryBuild(candidate, out map, out _))
                return true;
        return false;   // error holds the strict-parse failure
    }

    private static bool TryBuild(string json, out ProjectMap? map, out string error)
    {
        map = null;
        error = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(json, LenientJsonOptions);
            map = Build(doc.RootElement);
            return true;
        }
        catch (JsonException ex) { error = ex.Message; return false; }
        catch (KeyNotFoundException ex) { error = $"missing required field: {ex.Message}"; return false; }
        // p0426: and everything else. A boundary whose purpose is to turn "the model wrote
        // something odd" into a false must not keep a list of the odd things it expects;
        // run 27 died at step 12 on the third kind.
        catch (Exception ex) { error = $"unreadable project map: {ex.Message}"; return false; }
    }

    private static ProjectMap Build(JsonElement root) => new(
        PrimaryLanguage: Json.JsonValueReader.Text(root, "primary_language", "unknown")!,
        Frameworks: ReadStringArray(root, "frameworks"),
        Modules: ReadModules(root),
        TestProjects: ReadTestProjects(root),
        EntryPoints: ReadStringArray(root, "entry_points"),
        Conventions: ReadConventions(root),
        Ci: ReadCi(root),
        Prerequisites: Json.JsonValueReader.Text(root, "prerequisites"));

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
                Path: Json.JsonValueReader.Text(e, "path", "")!,
                Role: ParseRole(Json.JsonValueReader.Text(e, "role")),
                DependsOn: ReadStringArray(e, "depends_on"))).ToList();

    private static IReadOnlyList<TestProject> ReadTestProjects(JsonElement root) =>
        !root.TryGetProperty("test_projects", out var arr) || arr.ValueKind != JsonValueKind.Array
            ? []
            : arr.EnumerateArray().Select(e => new TestProject(
                Path: Json.JsonValueReader.Text(e, "path", "")!,
                Framework: Json.JsonValueReader.Text(e, "framework", "")!,
                FileCount: Json.JsonValueReader.Int32(e, "file_count"),
                SampleTestPath: Json.JsonValueReader.Text(e, "sample_test_path"))).ToList();

    private static Conventions ReadConventions(JsonElement root) =>
        !root.TryGetProperty("conventions", out var c) || c.ValueKind != JsonValueKind.Object
            ? new Conventions(null, null, null)
            : new Conventions(
                NamingPattern: Json.JsonValueReader.Text(c, "naming_pattern"),
                TestLayout: Json.JsonValueReader.Text(c, "test_layout"),
                ErrorHandling: Json.JsonValueReader.Text(c, "error_handling"));

    private static CiConfig ReadCi(JsonElement root) =>
        !root.TryGetProperty("ci", out var ci) || ci.ValueKind != JsonValueKind.Object
            ? new CiConfig(false, null, null, null)
            : new CiConfig(
                HasCi: ci.TryGetProperty("has_ci", out var h) && h.ValueKind is JsonValueKind.True,
                BuildCommand: ci.TryGetProperty("build_command", out var b) ? b.GetString() : null,
                TestCommand: ci.TryGetProperty("test_command", out var t) ? t.GetString() : null,
                CiSystem: ci.TryGetProperty("ci_system", out var s) ? s.GetString() : null);

    private static ModuleRole ParseRole(string? raw) => raw?.ToLowerInvariant() switch
    {
        "production" => ModuleRole.Production,
        "test" => ModuleRole.Test,
        "tool" => ModuleRole.Tool,
        "generated" => ModuleRole.Generated,
        _ => ModuleRole.Other
    };
}
