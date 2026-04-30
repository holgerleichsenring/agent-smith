using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using IRepositoryToolDispatcher = AgentSmith.Contracts.Services.IRepositoryToolDispatcher;

namespace AgentSmith.Application.Services;

/// <summary>
/// Runs the agentic analyzer over a repository and parses the model's terminal
/// JSON response into a ProjectMap. One retry on JSON-parse failure with the
/// parse error appended to the user prompt; failure after the retry surfaces
/// to the handler as an exception. No silent fallback.
/// </summary>
public sealed class ProjectAnalyzer(
    IAgenticAnalyzerFactory analyzerFactory,
    IPromptCatalog prompts,
    IRepositoryToolDispatcher toolDispatcher,
    ILogger<ProjectAnalyzer> logger) : IProjectAnalyzer
{
    private const int MaxIterations = 25;
    private static readonly IReadOnlyList<ToolDefinition> Tools = BuildTools();

    public async Task<ProjectMap> AnalyzeAsync(
        string repositoryPath, AgentConfig agent, CancellationToken cancellationToken)
    {
        var analyzer = analyzerFactory.Create(agent);
        var systemPrompt = prompts.Get("project-analyzer-system");
        var userPrompt = $"Repository to analyze: {repositoryPath}\n\nStart by listing the root directory.";

        var handler = new ReadOnlyToolCallHandler((name, input, ct) =>
            toolDispatcher.ExecuteAsync(repositoryPath, name, input, ct));

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var prompt = attempt == 1 ? userPrompt : userPrompt + LastAttemptError;
            var result = await analyzer.AnalyzeAsync(
                systemPrompt, prompt, Tools, handler, MaxIterations, cancellationToken);

            logger.LogInformation(
                "ProjectAnalyzer attempt {Attempt}: {Iter} iterations, {Calls} tool calls, {In}+{Out} tokens",
                attempt, result.Iterations, result.ToolCallCount,
                result.Tokens.Input, result.Tokens.Output);

            if (TryParse(result.FinalText, out var map, out var parseError))
                return map!;

            LastAttemptError = $"\n\nYour previous response could not be parsed as JSON: {parseError}\n" +
                "Respond again with ONLY the JSON object, no surrounding prose, no code fences.";
            logger.LogWarning(
                "ProjectAnalyzer attempt {Attempt} produced unparseable output: {Error}",
                attempt, parseError);
        }

        throw new InvalidOperationException(
            "ProjectAnalyzer failed after 2 attempts: model never produced parseable JSON. " +
            "Check logs for the raw responses; consider adjusting the analyzer prompt or upgrading the model.");
    }

    // Mutable per-call state — kept simple via instance field; the factory issues
    // a fresh ProjectAnalyzer per consumer (Singleton DI in handler-scoped use).
    private string LastAttemptError { get; set; } = string.Empty;

    private static bool TryParse(string finalText, out ProjectMap? map, out string error)
    {
        map = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(finalText))
        {
            error = "model returned empty response";
            return false;
        }

        // Strip optional code-fence wrappers in case the model included them despite the prompt.
        var json = finalText.Trim();
        if (json.StartsWith("```"))
        {
            var firstNl = json.IndexOf('\n');
            if (firstNl > 0) json = json[(firstNl + 1)..];
            if (json.EndsWith("```")) json = json[..^3].TrimEnd();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            map = JsonToProjectMap(doc.RootElement);
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (KeyNotFoundException ex)
        {
            error = $"missing required field: {ex.Message}";
            return false;
        }
    }

    private static ProjectMap JsonToProjectMap(JsonElement root) =>
        new(
            PrimaryLanguage: root.TryGetProperty("primary_language", out var lang) ? lang.GetString() ?? "unknown" : "unknown",
            Frameworks: ReadStringArray(root, "frameworks"),
            Modules: ReadModules(root),
            TestProjects: ReadTestProjects(root),
            EntryPoints: ReadStringArray(root, "entry_points"),
            Conventions: ReadConventions(root),
            Ci: ReadCi(root));

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
        return arr.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString() ?? string.Empty)
            .Where(s => s.Length > 0)
            .ToList();
    }

    private static IReadOnlyList<Module> ReadModules(JsonElement root)
    {
        if (!root.TryGetProperty("modules", out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
        return arr.EnumerateArray().Select(e => new Module(
            Path: e.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "",
            Role: ParseRole(e.TryGetProperty("role", out var r) ? r.GetString() : null),
            DependsOn: ReadStringArray(e, "depends_on"))).ToList();
    }

    private static IReadOnlyList<TestProject> ReadTestProjects(JsonElement root)
    {
        if (!root.TryGetProperty("test_projects", out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
        return arr.EnumerateArray().Select(e => new TestProject(
            Path: e.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "",
            Framework: e.TryGetProperty("framework", out var f) ? f.GetString() ?? "" : "",
            FileCount: e.TryGetProperty("file_count", out var c) && c.TryGetInt32(out var i) ? i : 0,
            SampleTestPath: e.TryGetProperty("sample_test_path", out var s) ? s.GetString() : null)).ToList();
    }

    private static Conventions ReadConventions(JsonElement root)
    {
        if (!root.TryGetProperty("conventions", out var c) || c.ValueKind != JsonValueKind.Object)
            return new Conventions(null, null, null);
        return new Conventions(
            NamingPattern: c.TryGetProperty("naming_pattern", out var n) ? n.GetString() : null,
            TestLayout: c.TryGetProperty("test_layout", out var tl) ? tl.GetString() : null,
            ErrorHandling: c.TryGetProperty("error_handling", out var eh) ? eh.GetString() : null);
    }

    private static CiConfig ReadCi(JsonElement root)
    {
        if (!root.TryGetProperty("ci", out var ci) || ci.ValueKind != JsonValueKind.Object)
            return new CiConfig(false, null, null, null);
        return new CiConfig(
            HasCi: ci.TryGetProperty("has_ci", out var h) && h.ValueKind is JsonValueKind.True,
            BuildCommand: ci.TryGetProperty("build_command", out var b) ? b.GetString() : null,
            TestCommand: ci.TryGetProperty("test_command", out var t) ? t.GetString() : null,
            CiSystem: ci.TryGetProperty("ci_system", out var s) ? s.GetString() : null);
    }

    private static ModuleRole ParseRole(string? raw) => raw?.ToLowerInvariant() switch
    {
        "production" => ModuleRole.Production,
        "test" => ModuleRole.Test,
        "tool" => ModuleRole.Tool,
        "generated" => ModuleRole.Generated,
        _ => ModuleRole.Other
    };

    private static IReadOnlyList<ToolDefinition> BuildTools() =>
    [
        new("list_files", "List files in a directory of the repository.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject { ["path"] = StringProp("Relative directory path, empty string for root.") },
                ["required"] = new JsonArray("path")
            }),
        new("read_file", "Read the contents of a file in the repository.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject { ["path"] = StringProp("Relative path from repository root.") },
                ["required"] = new JsonArray("path")
            }),
        new("grep", "Search files for a regex pattern. Returns up to 200 matching lines as JSON {matches:[{path,line,text}], truncated}.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["pattern"] = StringProp("Regex pattern to match against each line."),
                    ["glob"] = StringProp("Optional glob to limit which files are searched (e.g. '**/*.cs'). Defaults to '**/*'.")
                },
                ["required"] = new JsonArray("pattern")
            }),
    ];

    private static JsonObject StringProp(string description) =>
        new() { ["type"] = "string", ["description"] = description };
}
