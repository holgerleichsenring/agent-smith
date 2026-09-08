namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-09-08-1830: the contexts the scope call NAMED as touched by the ticket, with
/// the rationale it gave — the claim the derivation's cut is held against. Recorded on
/// every ticketed run, a single-repo one included; it narrows nothing, unlike the kept
/// set behind <c>ContextKeys.ScopedContexts</c>, which exists only when a context was
/// dropped. Run 8688 named two contexts, narrowed nothing, and was cut for one.
/// </summary>
/// <param name="Contexts">The identifiers as the derivation prompt lists them: bare
/// context names when one repository is in scope, <c>repo/context</c> otherwise. Only
/// names the inventory holds, from repositories with more than one context.</param>
/// <param name="Rationale">The scope call's one-line reason, verbatim; null when absent.</param>
public sealed record ScopeNamedContexts(IReadOnlyList<string> Contexts, string? Rationale = null)
{
    /// <summary>True when a declared context identifies one of the named ones — the
    /// same spelling, or the bare context name of a <c>repo/context</c> identifier.</summary>
    public static bool Matches(string named, string declared) =>
        string.Equals(named, declared, StringComparison.OrdinalIgnoreCase)
        || named.EndsWith("/" + declared, StringComparison.OrdinalIgnoreCase);
}
