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
/// </summary>
public sealed class DerivationLook
{
    private readonly IReadOnlyDictionary<string, ISandbox> _sandboxes;
    private readonly IList<AITool> _tools;

    public DerivationLook(
        IReadOnlyDictionary<string, ISandbox> sandboxes, ISandboxFileReaderFactory files,
        IPackageEcosystemDetector ecosystems, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(sandboxes);
        _sandboxes = sandboxes;
        _tools = DerivationTools.Over(
            new RepositorySearchTool(this, logger),
            new RepositoryFileReadTool(this, files, logger),
            new DependencyAuditTool(this, files, ecosystems, logger));
    }

    /// <summary>The repositories a look may name, for the prompt to list.</summary>
    public IReadOnlyList<string> Repositories => [.. _sandboxes.Keys];

    /// <summary>One allowance for the whole derivation — never re-opened.</summary>
    public DerivationLookBudget Budget { get; } = new();

    /// <summary>Every look taken, one framework-minted line with an id each.</summary>
    public DerivationEvidence Evidence { get; } = new();

    /// <summary>The named read-only tools, each bounded in what it may return.</summary>
    public IList<AITool> Tools => _tools;

    /// <summary>Takes one look from the budget and resolves the repository; a refusal is
    /// the sentence the model is answered with, and nothing has run.</summary>
    internal bool TryOpen(string repository, out ISandbox sandbox, out string refusal)
    {
        sandbox = null!;
        refusal = string.Empty;
        if (!Budget.TryTake())
        {
            refusal = DerivationLookBudget.Exhausted;
            return false;
        }
        if (_sandboxes.TryGetValue(repository, out var found))
        {
            sandbox = found;
            return true;
        }
        refusal = $"No repository named '{repository}'. The run carries: {string.Join(", ", _sandboxes.Keys)}.";
        return false;
    }
}
