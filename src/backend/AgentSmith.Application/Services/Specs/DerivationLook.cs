using AgentSmith.Application.Services.Turns;
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
/// 2026-09-13-84c0: a declared template is one more name, on its own allowance (see
/// <see cref="DerivationLookGate"/>). The look owns the scopes it was handed and disposes them.
/// </para>
/// <para>
/// 2026-09-15-ffa7: a look has a holder. The cut reviewer is handed one of its own, per
/// attempt, on <see cref="DerivationLookTerms.CutReview"/>.
/// </para>
/// </summary>
public sealed class DerivationLook : IAsyncDisposable
{
    private readonly IReadOnlyDictionary<string, ISourceScopeSandbox> _templates;
    private readonly DerivationLookGate _gate;

    public DerivationLook(
        IReadOnlyDictionary<string, ISandbox> sandboxes, ISandboxFileReaderFactory files,
        IPackageEcosystemDetector ecosystems, ILogger logger,
        IReadOnlyDictionary<string, ISourceScopeSandbox>? templates = null,
        DerivationLookTerms? terms = null, bool audits = true,
        TurnActivityTools? activity = null)
    {
        ArgumentNullException.ThrowIfNull(sandboxes);
        _templates = templates ?? new Dictionary<string, ISourceScopeSandbox>(StringComparer.Ordinal);
        Terms = terms ?? DerivationLookTerms.Derivation;
        _gate = new DerivationLookGate(sandboxes, _templates, Terms);
        Evidence = new DerivationEvidence(Terms.EvidencePrefix, $"the {Terms.Actor}");
        var tools = DerivationTools.Over(
            new RepositorySearchTool(this, logger),
            new RepositoryFileReadTool(this, files, logger),
            audits ? new DependencyAuditTool(this, files, ecosystems, logger) : null);
        // 2026-09-17-042ee: a look reports what it reads to whoever set an observer. Nobody
        // has during a derivation; a design turn's proposal review runs inside one that has.
        Tools = activity?.Reporting(tools) ?? tools;
    }

    /// <summary>2026-09-15-ffa7: whose look this is — its allowance, its id letter, its name.</summary>
    public DerivationLookTerms Terms { get; }

    /// <summary>The repositories a look may name, for the prompt to list.</summary>
    public IReadOnlyList<string> Repositories => [.. _gate.Names.Except(_templates.Keys)];

    /// <summary>The template entries a look may name, listed apart from the targets.
    /// 2026-09-13-9f84: the scopes themselves, because the framework reads what each one
    /// declares as its own proof and a name cannot be read from.</summary>
    public IReadOnlyDictionary<string, ISourceScopeSandbox> Templates => _templates;

    /// <summary>One allowance for the whole look — never re-opened.</summary>
    public DerivationLookBudget Budget => _gate.Budget;

    /// <summary>A template's own allowance, apart from the targets'.</summary>
    public DerivationLookBudget TemplateBudget => _gate.TemplateBudget;

    /// <summary>Set when a TEMPLATE look was refused for budget; the deriver fails on it.</summary>
    public string? TemplateRefusal => _gate.TemplateRefusal;

    /// <summary>Every look taken, one framework-minted line with an id each.</summary>
    public DerivationEvidence Evidence { get; }

    /// <summary>The named read-only tools, each bounded in what it may return. 2026-09-17-042ed:
    /// the audit is an input — a proposal review over read-only scopes cannot run one.</summary>
    public IList<AITool> Tools { get; }

    /// <summary>Resolves the repository, THEN takes from the allowance that name belongs to;
    /// a refusal is the sentence the model is answered with, and nothing has run.</summary>
    internal bool TryOpen(string repository, out ISandbox sandbox, out string refusal) =>
        _gate.TryOpen(repository, out sandbox, out refusal);

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _templates.Values) await scope.DisposeAsync();
    }
}
