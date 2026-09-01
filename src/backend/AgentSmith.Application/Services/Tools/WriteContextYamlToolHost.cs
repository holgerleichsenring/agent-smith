using System.ComponentModel;
using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
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

    private readonly IReadOnlyDictionary<string, ISandbox> _sandboxes;
    private readonly string _defaultRepo;
    private readonly IContextYamlSerializer _serializer;
    private readonly ContextDocumentGate _gate;
    // 2026-08-26-364f: the file is read before it is written, so the sections the typed
    // document does not model survive a re-init instead of being deleted by it.
    private readonly SandboxContextYamlWriter _writer;
    // 2026-09-01-e14d: hashes the files a written verify block says it was derived from.
    private readonly VerifyDerivationStamp _derivation;
    // 2026-08-25-c9c7: per-round, so a document the model cannot make valid stops
    // being re-invited instead of spending the loop's iteration cap.
    private readonly ContextWriteRejectionBudget _budget = new();
    // p0341c: the discovered context keys per repo NAME (from ScopeRepos'
    // RemoteContextInventory), + the default repo's name, so context_name is constrained
    // to what discovery actually resolved. Null / empty for a repo => genuine bootstrap,
    // any name allowed.
    private readonly ContextNameGuard _nameGuard;
    // 2026-08-26-167c: what THIS round did, so the round can stop deciding by
    // "a file exists" — a question a re-init answers yes to before it starts.
    private bool _written;
    private string? _lastRefusal;
    private string? _lastContext;

    public WriteContextYamlToolHost(
        IReadOnlyDictionary<string, ISandbox> sandboxes,
        string defaultRepo,
        IContextYamlSerializer serializer,
        ContextDocumentGate gate,
        SandboxContextYamlWriter writer,
        VerifyDerivationStamp derivation,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? discoveredContexts = null,
        string? defaultRepoName = null)
    {
        _sandboxes = sandboxes;
        _defaultRepo = defaultRepo;
        _serializer = serializer;
        _gate = gate;
        _writer = writer;
        _derivation = derivation;
        _nameGuard = new ContextNameGuard(discoveredContexts, defaultRepoName);
    }

    /// <summary>This round's write outcome — never a question about the disk.</summary>
    public ContextWriteOutcome Outcome => new(
        _written, _lastRefusal,
        _lastContext is not null && _budget.IsExhausted(_lastContext));

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
        // 2026-08-26-04b6: the description asks for JUDGEMENT and MECHANISM only. A reading —
        // a value the repository still states for itself — is accepted and then discarded, so a
        // model working from an older prompt is not punished for offering one.
        [Description("Document object: { meta: { workdir, type?: [archetype,…], purpose? }, " +
                     "stack?: { lang?, image?, resources? }, " +
                     "verify?: [ { label, command, when_present? }, … ], " +
                     "verify_derived_from?: { files: [path,…] }, " +
                     "arch?: object, quality?: object, behavior?: object }. " +
                     "Do NOT restate what the repository already states about itself — the build " +
                     "file's frameworks, versions and packages, the workflow's CI platform, the " +
                     "folder names as layers. Those are dropped. State what somebody DECIDED " +
                     "(meta.purpose, quality.limits, behavior) and what the orchestrator ACTS ON " +
                     "(meta.workdir, stack.lang, stack.image). " +
                     "meta.workdir is REQUIRED — '.' for single-stack, otherwise the sub-tree path. " +
                     "stack.image is REQUIRED whenever a stack is present — the exact toolchain Docker image " +
                     "whose runtime can BOTH build AND run this stack's tests (e.g. mcr.microsoft.com/dotnet/sdk:8.0, " +
                     "node:20-bookworm); it must come from a registry the operator trusts and must carry git, " +
                     "because the repository is cloned inside it. " +
                     // p0332: resources demoted to the exception — the defaults fit
                     // almost every stack; agents must stop sizing every context.yaml.
                     "stack.resources is NORMALLY OMITTED — the platform defaults fit almost every stack, " +
                     "including real dotnet/Roslyn and npm builds. Declare it only for a defensible outlier: " +
                     "a build that DEMONSTRABLY needs more than the default (e.g. it OOM-killed or you measured " +
                     "the peak). If you declare it, provide ALL FOUR Kubernetes quantities { cpu_request, " +
                     "cpu_limit, memory_request, memory_limit } — a partial block is refused — and values above " +
                     "the hard ceiling (cpu '2', memory '6Gi') are clamped down to it. " +
                     // 2026-08-31-26d4: the gate the repository owns, ahead of anything a
                     // model emits for a single run.
                     "verify is the ORDERED list of commands that prove a change in this context holds — " +
                     "each { label, command, when_present? }, run at this context's workdir, stopping at " +
                     "the first non-zero exit. Every command must be able to FAIL: a declared 'echo ...' " +
                     "or 'true' stops the run at resolution. Use when_present for a stage that only means " +
                     "something when a path exists; an absent path skips that stage instead of reddening " +
                     "it. Omit verify for a .NET tree — its entry point is discovered from files that exist. " +
                     // 2026-09-01-e14d: adoption, not invention — and a record of what was adopted.
                     "DERIVE those commands from what this repository already runs: its CI definition " +
                     "(azure-pipelines.yml, .github/workflows/*, .gitlab-ci.yml, Jenkinsfile), its " +
                     "Makefile and scripts, its manifests and task runners — the same reading you do " +
                     "to work out the build command. Name the files you read them out of in " +
                     "verify_derived_from.files (paths relative to meta.workdir); the framework " +
                     "hashes those files itself, so send no hash. A repository whose pipeline you " +
                     "could not find gets NO verify block and no verify_derived_from — an invented " +
                     "gate disagrees with the one the estate actually runs.")]
        JsonElement document,
        CancellationToken ct = default)
    {
        _lastContext = context_name;
        var message = await AttemptAsync(repo, context_name, document, ct);
        if (message.StartsWith(SandboxContextYamlWriter.WrittenPrefix, StringComparison.Ordinal)) _written = true;
        else _lastRefusal = message;
        return message;
    }

    private async Task<string> AttemptAsync(
        string repo, string context_name, JsonElement document, CancellationToken ct)
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

        if (!TryResolveSandbox(repo, out var sandbox, out var err))
            return err!;
        // 2026-09-01-e14d: stamped BEFORE the document is serialised or judged, so the hash
        // is in the file the schema validates and the file that reaches disk.
        typed = await _derivation.StampAsync(typed!, sandbox!, ct);

        // Serialize first: it validates the fundamental meta.workdir requirement.
        string yaml;
        try { yaml = _serializer.Serialize(typed); }
        catch (InvalidOperationException ex) { return _budget.Reject(context_name, ex.Message); }

        if (_gate.Defect(typed) is { } defect) return _budget.Reject(context_name, defect);
        _budget.Accepted(context_name);

        return await _writer.WriteAsync(sandbox!, repo, context_name, yaml, ct);
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
