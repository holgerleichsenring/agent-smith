using System.Text.Json;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
// Was: using IRepositoryToolDispatcher = AgentSmith.Contracts.Services.IRepositoryToolDispatcher;
// IRepositoryToolDispatcher is replaced by SandboxToolHost in p0119a.

namespace AgentSmith.Application.Services;

/// <summary>
/// Drives a Microsoft.Extensions.AI IChatClient with the SandboxToolHost scout
/// tool subset to produce a ProjectMap. One retry on JSON-parse failure with the
/// parse error appended to the user prompt; failure after the retry surfaces
/// to the handler as an exception. No silent fallback.
/// </summary>
public sealed class ProjectAnalyzer(
    IChatClientFactory chatClientFactory,
    IPromptCatalog prompts,
    IDecisionLogger decisionLogger,
    ILogger<ProjectAnalyzer> logger) : IProjectAnalyzer
{
    private const int MaximumIterations = 25;

    private static readonly JsonDocumentOptions LenientJsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public async Task<ProjectMap> AnalyzeAsync(
        string repositoryPath, AgentConfig agent, ISandbox sandbox, CancellationToken cancellationToken)
    {
        var systemPrompt = prompts.Get("project-analyzer-system");
        var userPrompt = $"Repository to analyze: {repositoryPath}\n\nStart by listing the root directory.";

        var toolHost = new SandboxToolHost(sandbox, decisionLogger, repoPath: repositoryPath);
        var chat = chatClientFactory.Create(agent, TaskType.Primary);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(agent, TaskType.Primary);

        var lastError = string.Empty;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var prompt = attempt == 1
                ? userPrompt
                : userPrompt +
                  $"\n\nYour previous response could not be parsed as JSON: {lastError}\n" +
                  "Respond again with ONLY the JSON object, no surrounding prose, no code fences.";

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, prompt),
            };
            var options = new ChatOptions
            {
                Tools = toolHost.GetScoutTools(),
                MaxOutputTokens = maxTokens,
                // iter cap on factory
            };
            var response = await chat.GetResponseAsync(messages, options, cancellationToken);

            logger.LogInformation(
                "ProjectAnalyzer attempt {Attempt}: {In}+{Out} tokens",
                attempt,
                response.Usage?.InputTokenCount ?? 0,
                response.Usage?.OutputTokenCount ?? 0);

            if (TryParse(response.Text ?? string.Empty, out var map, out var parseError))
                return map!;

            lastError = parseError;
            logger.LogWarning(
                "ProjectAnalyzer attempt {Attempt} produced unparseable output: {Error}",
                attempt, parseError);
        }

        throw new InvalidOperationException(
            "ProjectAnalyzer failed after 2 attempts: model never produced parseable JSON. " +
            "Check logs for the raw responses; consider adjusting the analyzer prompt or upgrading the model.");
    }

    private static bool TryParse(string finalText, out ProjectMap? map, out string error)
    {
        map = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(finalText))
        {
            error = "model returned empty response";
            return false;
        }

        var json = finalText.Trim();
        if (json.StartsWith("```"))
        {
            var firstNl = json.IndexOf('\n');
            if (firstNl > 0) json = json[(firstNl + 1)..];
            if (json.EndsWith("```")) json = json[..^3].TrimEnd();
        }

        try
        {
            using var doc = JsonDocument.Parse(json, LenientJsonOptions);
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
}
