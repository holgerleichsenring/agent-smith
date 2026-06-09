using System.ComponentModel;
using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0193: typed write path for .agentsmith/contexts/&lt;name&gt;/context.yaml.
/// The agent supplies a JSON document; the host deserializes to the typed
/// ContextYamlDocument and emits YAML through IContextYamlSerializer (the
/// same builder the parser uses on read). Parse-failure from LLM output
/// becomes unrepresentable.
/// </summary>
public sealed class WriteContextYamlToolHost : IToolHost
{
    public const string ToolName = "write_context_yaml";
    private const int WriteTimeoutSeconds = 30;

    private readonly IReadOnlyDictionary<string, ISandbox> _sandboxes;
    private readonly string _defaultRepo;
    private readonly IContextYamlSerializer _serializer;

    public WriteContextYamlToolHost(
        IReadOnlyDictionary<string, ISandbox> sandboxes,
        string defaultRepo,
        IContextYamlSerializer serializer)
    {
        _sandboxes = sandboxes;
        _defaultRepo = defaultRepo;
        _serializer = serializer;
    }

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(WriteContextYaml, name: ToolName)];
    }

    [Description(
        "Writes .agentsmith/contexts/{context_name}/context.yaml in the named repo. " +
        "Pass a structured JSON document — the framework serialises to YAML with " +
        "correct quoting, so values like '@scope/pkg' or 'key: value' never break " +
        "the YAML scanner. Always use this instead of write_file for context.yaml.")]
    public async Task<string> WriteContextYaml(
        [Description("Repository name (must be one of the run's repos). Use the empty string for single-repo runs.")]
        string repo,
        [Description("Context name, e.g. 'default' or 'api'. Becomes the directory under .agentsmith/contexts/.")]
        string context_name,
        [Description("Document object: { meta: { workdir, project?, version?, type?, purpose? }, " +
                     "stack?: { lang?, image?, runtime?, infra?, testing?, frameworks?, sdks? }, " +
                     "arch?: object, quality?: object, behavior?: object }. " +
                     "meta.workdir is REQUIRED — '.' for single-stack, otherwise the sub-tree path. " +
                     "stack.image is the exact toolchain Docker image whose runtime can BOTH build " +
                     "AND run this stack's tests (e.g. mcr.microsoft.com/dotnet/sdk:8.0, node:20-bookworm); " +
                     "name it from a trusted hub and pick a git-bearing tag (full -bookworm/-bullseye, an " +
                     "mcr .../sdk tag, or buildpack-deps:...-scm — never -slim/-alpine).")]
        JsonElement document,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(context_name))
            return "Error: context_name is required.";
        if (context_name.Contains('/') || context_name.Contains('\\') || context_name.Contains(".."))
            return $"Error: context_name '{context_name}' must be a single path segment (no slashes, no '..').";

        ContextYamlDocument typed;
        try
        {
            typed = JsonSerializer.Deserialize<ContextYamlDocument>(document.GetRawText(), DocumentJsonOptions)
                ?? throw new InvalidOperationException("document deserialised to null");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return $"Error: document is not a valid context.yaml shape — {ex.Message}";
        }

        string yaml;
        try { yaml = _serializer.Serialize(typed); }
        catch (InvalidOperationException ex) { return $"Error: {ex.Message}"; }

        if (!TryResolveSandbox(repo, out var sandbox, out var err))
            return err!;

        var path = $".agentsmith/contexts/{context_name}/context.yaml";
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteFile,
            TimeoutSeconds: WriteTimeoutSeconds, Path: path, Content: yaml);
        var result = await sandbox!.RunStepAsync(step, progress: null, ct);
        return result.ExitCode != 0
            ? $"Error: write failed — {result.ErrorMessage ?? "unknown"}"
            : $"context.yaml written: {(string.IsNullOrEmpty(repo) ? string.Empty : repo + "/")}{path}";
    }

    private bool TryResolveSandbox(string repo, out ISandbox? sandbox, out string? error)
    {
        var key = string.IsNullOrEmpty(repo) ? _defaultRepo : repo;
        if (_sandboxes.TryGetValue(key, out sandbox))
        {
            error = null;
            return true;
        }
        sandbox = null;
        error = $"Error: unknown repo '{repo}'. Known repos: [{string.Join(", ", _sandboxes.Keys.Where(k => k.Length > 0))}].";
        return false;
    }

    private static readonly JsonSerializerOptions DocumentJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
