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
/// 2026-08-25-c9c7: the document is also judged by <see cref="ContextDocumentGate"/>
/// before it reaches disk, and every refusal is spent from a per-round
/// <see cref="ContextWriteRejectionBudget"/>.
/// </summary>
public sealed class WriteContextYamlToolHost : IToolHost
{
    public const string ToolName = "write_context_yaml";
    private const int WriteTimeoutSeconds = 30;

    private readonly IReadOnlyDictionary<string, ISandbox> _sandboxes;
    private readonly string _defaultRepo;
    private readonly IContextYamlSerializer _serializer;
    private readonly ContextDocumentGate _gate;
    // 2026-08-25-c9c7: per-round, so a document the model cannot make valid stops
    // being re-invited instead of spending the loop's iteration cap.
    private readonly ContextWriteRejectionBudget _budget = new();
    // p0341c: the discovered context keys per repo NAME (from ScopeRepos'
    // RemoteContextInventory), + the default repo's name, so context_name is constrained
    // to what discovery actually resolved. Null / empty for a repo => genuine bootstrap,
    // any name allowed.
    private readonly ContextNameGuard _nameGuard;

    public WriteContextYamlToolHost(
        IReadOnlyDictionary<string, ISandbox> sandboxes,
        string defaultRepo,
        IContextYamlSerializer serializer,
        ContextDocumentGate gate,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? discoveredContexts = null,
        string? defaultRepoName = null)
    {
        _sandboxes = sandboxes;
        _defaultRepo = defaultRepo;
        _serializer = serializer;
        _gate = gate;
        _nameGuard = new ContextNameGuard(discoveredContexts, defaultRepoName);
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
        [Description("Document object: { meta: { workdir, project?, version?, type?: [archetype,…], purpose?, domain? }, " +
                     "stack?: { lang?, image?, resources?, runtime?, infra?, testing?, frameworks?, sdks? }, " +
                     "arch?: object, quality?: object, behavior?: object }. " +
                     "meta.workdir is REQUIRED — '.' for single-stack, otherwise the sub-tree path. " +
                     "meta.domain is OPTIONAL: one word naming a profile that supplies this context's " +
                     "toolchain image and verification commands; a context declaring one may omit " +
                     "stack.image. " +
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
                     "memory_limit } — a partial block is refused — and values above the hard ceiling (cpu '2', memory '6Gi') are " +
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
        if (!_nameGuard.TryResolve(repo, ref context_name, out var guardError))
            return guardError!;

        // 2026-08-25-c9c7: every refusal below is budgeted, so no defect can be
        // re-offered to the model past the bound.
        if (!_gate.TryRead(document, out var typed, out var readDefect))
            return _budget.Reject(context_name, readDefect!);

        // Serialize first: it validates the fundamental meta.workdir requirement.
        string yaml;
        try { yaml = _serializer.Serialize(typed!); }
        catch (InvalidOperationException ex) { return _budget.Reject(context_name, ex.Message); }

        if (_gate.Defect(typed!) is { } defect) return _budget.Reject(context_name, defect);
        _budget.Accepted(context_name);

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
}
