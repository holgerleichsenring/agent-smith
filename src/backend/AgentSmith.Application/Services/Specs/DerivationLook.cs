using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: the derivation's one chance to LOOK before it writes the work order.
/// <para>
/// A dependency ticket derived the criterion that the manifests must carry upgraded
/// versions; for days that was taken as unsatisfiable on the belief that every finding
/// was transitive. The ecosystem's own audit says otherwise, and the derivation could not
/// ask it: it had ticket prose and a code map, and neither carries an advisory. So it is
/// handed a fixed, named set of read-only tools — a search, a file read, the ecosystem's
/// audit — and which to call is its choice. What CAN be called is the framework's.
/// </para>
/// <para>
/// One host serves every attempt of the deriver's retry loop, so the budget and the
/// evidence ids span the attempts: a retry sees the looks it already took.
/// </para>
/// <para>
/// 2026-09-13-84c0: a declared template is one more name, on its OWN allowance and in a
/// COPY of the map — never in the run's, which CommitAndPR and the toolchain probe iterate.
/// The look owns the scopes it was handed and disposes them.
/// </para>
/// </summary>
public sealed class DerivationLook : IAsyncDisposable
{
    private readonly IReadOnlyDictionary<string, ISandbox> _sandboxes;
    private readonly IReadOnlyDictionary<string, ISourceScopeSandbox> _templates;
    private readonly IList<AITool> _tools;

    public DerivationLook(
        IReadOnlyDictionary<string, ISandbox> sandboxes, ISandboxFileReaderFactory files,
        IPackageEcosystemDetector ecosystems, ILogger logger,
        IReadOnlyDictionary<string, ISourceScopeSandbox>? templates = null)
    {
        ArgumentNullException.ThrowIfNull(sandboxes);
        _templates = templates ?? new Dictionary<string, ISourceScopeSandbox>(StringComparer.Ordinal);
        _sandboxes = Merge(sandboxes, _templates);
        _tools = DerivationTools.Over(
            new RepositorySearchTool(this, logger),
            new RepositoryFileReadTool(this, files, logger),
            new DependencyAuditTool(this, files, ecosystems, logger));
    }

    /// <summary>The repositories a look may name, for the prompt to list.</summary>
    public IReadOnlyList<string> Repositories => [.. _sandboxes.Keys.Except(_templates.Keys)];

    /// <summary>The template entries a look may name, listed apart from the targets.</summary>
    public IReadOnlyList<string> Templates => [.. _templates.Keys];

    /// <summary>One allowance for the whole derivation — never re-opened.</summary>
    public DerivationLookBudget Budget { get; } = new();

    /// <summary>
    /// A template's own allowance. AccountSearchBudget records what one shared pool costs:
    /// the first repository spent all twelve and every later question was answered "No
    /// search left", and its own comment says a bigger shared pool does not fix that
    /// because it is still one pool.
    /// </summary>
    public DerivationLookBudget TemplateBudget { get; } = new();

    /// <summary>
    /// Set when a TEMPLATE look was refused for budget. A refusal sentence would let the
    /// derivation cut against the target's habits while the declaration says a template
    /// governs — a provenance the run record would carry as true. The deriver fails on it.
    /// </summary>
    public string? TemplateRefusal { get; private set; }

    /// <summary>Every look taken, one framework-minted line with an id each.</summary>
    public DerivationEvidence Evidence { get; } = new();

    /// <summary>The named read-only tools, each bounded in what it may return.</summary>
    public IList<AITool> Tools => _tools;

    /// <summary>Resolves the repository, THEN takes from the allowance that name belongs to;
    /// a refusal is the sentence the model is answered with, and nothing has run.</summary>
    internal bool TryOpen(string repository, out ISandbox sandbox, out string refusal)
    {
        sandbox = null!;
        refusal = string.Empty;
        // 2026-09-13-84c0: resolve BEFORE charging. Which budget to charge cannot be chosen
        // before the name is known to be a template or a target — and a mis-addressed guess
        // now costs the target nothing, where it used to cost it a look.
        if (!_sandboxes.TryGetValue(repository, out var found))
        {
            refusal = $"No repository named '{repository}'. The run carries: "
                + $"{string.Join(", ", _sandboxes.Keys)}.";
            return false;
        }
        var isTemplate = _templates.ContainsKey(repository);
        if (!(isTemplate ? TemplateBudget : Budget).TryTake())
        {
            refusal = DerivationLookBudget.Exhausted;
            if (isTemplate) TemplateRefusal ??= $"'{repository}': {refusal}";
            return false;
        }
        sandbox = found;
        return true;
    }

    private static Dictionary<string, ISandbox> Merge(
        IReadOnlyDictionary<string, ISandbox> targets,
        IReadOnlyDictionary<string, ISourceScopeSandbox> templates)
    {
        // A COPY, and the targets go in first so Keys.First() elsewhere is unmoved.
        var merged = new Dictionary<string, ISandbox>(targets, StringComparer.Ordinal);
        foreach (var (name, scope) in templates) merged[name] = scope;
        return merged;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _templates.Values) await scope.DisposeAsync();
    }
}
