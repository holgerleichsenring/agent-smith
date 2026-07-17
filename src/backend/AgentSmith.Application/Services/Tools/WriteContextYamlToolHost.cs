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
    // p0341c: the discovered context keys per repo NAME (from ScopeRepos'
    // RemoteContextInventory), + the default repo's name, so context_name is constrained
    // to what discovery actually resolved. Null / empty for a repo => genuine bootstrap,
    // any name allowed.
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>>? _discoveredContexts;
    private readonly string? _defaultRepoName;

    public WriteContextYamlToolHost(
        IReadOnlyDictionary<string, ISandbox> sandboxes,
        string defaultRepo,
        IContextYamlSerializer serializer,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? discoveredContexts = null,
        string? defaultRepoName = null)
    {
        _sandboxes = sandboxes;
        _defaultRepo = defaultRepo;
        _serializer = serializer;
        _discoveredContexts = discoveredContexts;
        _defaultRepoName = defaultRepoName;
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
                     "stack?: { lang?, image?, resources?, runtime?, infra?, testing?, frameworks?, sdks? }, " +
                     "arch?: object, quality?: object, behavior?: object }. " +
                     "meta.workdir is REQUIRED — '.' for single-stack, otherwise the sub-tree path. " +
                     "stack.image is REQUIRED whenever a stack is present — the exact toolchain Docker " +
                     "image whose runtime can BOTH build " +
                     "AND run this stack's tests (e.g. mcr.microsoft.com/dotnet/sdk:8.0, node:20-bookworm); " +
                     "name it from a trusted hub and pick a git-bearing tag (full -bookworm/-bullseye, an " +
                     "mcr .../sdk tag, or buildpack-deps:...-scm — never -slim/-alpine). " +
                     // p0332: resources demoted to the exception — the defaults fit
                     // almost every stack; agents must stop sizing every context.yaml.
                     "stack.resources is NORMALLY OMITTED — the platform defaults fit almost every " +
                     "stack, including real dotnet/Roslyn and npm builds. Declare it only for a " +
                     "defensible outlier: a build that DEMONSTRABLY needs more than the default " +
                     "(e.g. it OOM-killed or you measured the peak). If you declare it, provide ALL " +
                     "FOUR Kubernetes quantities { cpu_request, cpu_limit, memory_request, " +
                     "memory_limit } — a partial block is ignored and the project/global default " +
                     "applies — and values above the hard ceiling (cpu '2', memory '6Gi') are " +
                     "clamped down to it.")]
        JsonElement document,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(context_name))
            return "Error: context_name is required.";
        if (context_name.Contains('/') || context_name.Contains('\\') || context_name.Contains(".."))
            return $"Error: context_name '{context_name}' must be a single path segment (no slashes, no '..').";

        // p0341c: constrain context_name to the repo's DISCOVERED contexts — the invariant
        // belongs in the write API, not the prompt. An invented name (e.g. the example
        // 'default') when real contexts exist is rejected, or redirected when there is
        // exactly one real context. A genuine bootstrap (no discovered contexts) is
        // unaffected.
        if (!TryGuardContextName(repo, ref context_name, out var guardError))
            return guardError!;

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

        // Serialize first: it validates the fundamental meta.workdir requirement.
        string yaml;
        try { yaml = _serializer.Serialize(typed); }
        catch (InvalidOperationException ex) { return $"Error: {ex.Message}"; }

        // stack.image is mandatory whenever a stack is described: the toolchain
        // image the sandbox builds + tests this stack in must be named explicitly,
        // not left to the weaker language→image fallback table. Fail loud so the
        // agent re-emits with an exact image rather than silently shipping a
        // context.yaml the resolver has to guess an image for.
        if (typed.Stack is not null && string.IsNullOrWhiteSpace(typed.Stack.Image))
            return "Error: stack.image is required — name the exact toolchain Docker image whose "
                 + "runtime can BOTH build AND run this stack's tests (e.g. "
                 + "mcr.microsoft.com/dotnet/sdk:8.0, node:20-bookworm). Pick a git-bearing tag "
                 + "(full -bookworm/-bullseye, an mcr .../sdk tag, or buildpack-deps:...-scm — "
                 + "never -slim/-alpine).";

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

    // p0341c: validate context_name against the target repo's discovered context keys.
    // Returns false (with an error) when the name is invented and cannot be safely
    // redirected; may REWRITE context_name to the single discovered context.
    private bool TryGuardContextName(string repo, ref string contextName, out string? error)
    {
        error = null;
        if (_discoveredContexts is null || _discoveredContexts.Count == 0) return true; // bootstrap

        var repoName = string.IsNullOrEmpty(repo) ? (_defaultRepoName ?? string.Empty) : repo;
        if (!_discoveredContexts.TryGetValue(repoName, out var keys) || keys is null || keys.Count == 0)
            return true; // no discovery for this repo => genuine bootstrap, any name allowed

        var requested = contextName; // ref params cannot be captured in a lambda
        if (keys.Any(k => string.Equals(k, requested, StringComparison.OrdinalIgnoreCase)))
            return true; // the model named a real discovered context

        if (keys.Count == 1)
        {
            // Exactly one real context — redirect the invented name to it rather than
            // authoring a stray sibling.
            contextName = keys[0];
            return true;
        }

        error = $"Error: context_name '{contextName}' is not a discovered context for repo "
            + $"'{(string.IsNullOrEmpty(repoName) ? "(default)" : repoName)}'. Use one of the "
            + $"resolved contexts: [{string.Join(", ", keys)}]. Do not invent a new context name "
            + "(e.g. the example 'default') when real contexts exist.";
        return false;
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
        // Deserialize arch/quality/behavior (IDictionary<string, object?>) into plain
        // CLR types, not JsonElement, so the YAML serializer emits real values instead
        // of a `value_kind: String` type wrapper.
        Converters = { new InferredTypeJsonConverter() },
    };
}
