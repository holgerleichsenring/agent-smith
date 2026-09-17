using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-15-ffa7: what a look lets through — a name resolved to a sandbox, charged to the
/// allowance that name belongs to. Split from <see cref="DerivationLook"/>, which was at the
/// file-length ceiling when its allowance stopped being one constant.
/// <para>
/// 2026-09-13-84c0: a declared template is one more name, on its OWN allowance and in a
/// COPY of the map — never in the run's, which CommitAndPR and the toolchain probe iterate.
/// </para>
/// </summary>
internal sealed class DerivationLookGate
{
    private readonly IReadOnlyDictionary<string, ISandbox> _sandboxes;
    private readonly IReadOnlySet<string> _templates;

    public DerivationLookGate(
        IReadOnlyDictionary<string, ISandbox> targets,
        IReadOnlyDictionary<string, ISourceScopeSandbox> templates, DerivationLookTerms terms)
    {
        _sandboxes = Merge(targets, templates);
        _templates = new HashSet<string>(templates.Keys, StringComparer.Ordinal);
        Budget = new DerivationLookBudget(terms);
        TemplateBudget = new DerivationLookBudget(terms);
    }

    /// <summary>Every name the gate resolves, targets first.</summary>
    public IEnumerable<string> Names => _sandboxes.Keys;

    /// <summary>One allowance for the targets — never re-opened.</summary>
    public DerivationLookBudget Budget { get; }

    /// <summary>
    /// A template's own allowance. AccountSearchBudget records what one shared pool costs:
    /// the first repository spent all twelve and every later question was answered "No
    /// search left", and its own comment says a bigger shared pool does not fix that
    /// because it is still one pool.
    /// </summary>
    public DerivationLookBudget TemplateBudget { get; }

    /// <summary>
    /// Set when a TEMPLATE look was refused for budget. A refusal sentence would let the
    /// derivation cut against the target's habits while the declaration says a template
    /// governs — a provenance the run record would carry as true. The deriver fails on it.
    /// </summary>
    public string? TemplateRefusal { get; private set; }

    /// <summary>Resolves the repository, THEN takes from the allowance that name belongs to;
    /// a refusal is the sentence the model is answered with, and nothing has run.</summary>
    public bool TryOpen(string repository, out ISandbox sandbox, out string refusal)
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
        var budget = _templates.Contains(repository) ? TemplateBudget : Budget;
        if (!budget.TryTake())
        {
            refusal = budget.Exhausted;
            if (budget == TemplateBudget) TemplateRefusal ??= $"'{repository}': {refusal}";
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
}
