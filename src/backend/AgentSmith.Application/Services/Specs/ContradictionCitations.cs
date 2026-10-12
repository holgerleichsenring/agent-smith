using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-3a36b: resolves the ids a CONTRADICTION hand-back cites against the evidence lines
/// the framework minted for this derivation's looks — the same index a phase fact is resolved
/// through. A contradiction is a claim about the repository; one that names no look this
/// derivation took, or an id nobody minted, has nothing a checker could re-read, and the deriver
/// asks for it again.
/// </summary>
public static class ContradictionCitations
{
    /// <param name="Lines">The minted lines the citations resolved to, in citation order.</param>
    /// <param name="Problem">Why the citations do not stand, as the re-ask says it; null when
    /// they do, and for every hand-back that is not a contradiction.</param>
    public sealed record Resolved(IReadOnlyList<string> Lines, string? Problem);

    /// <summary>The shape a contradiction is asked for, named in every re-ask.</summary>
    public const string Shape =
        "{\"case\": \"requirements_contradict_repository\", \"reason\": \"<what the repository shows>\", "
        + "\"cites\": [\"<the evidence id of each look that shows it>\"]}";

    public static Resolved Resolve(
        SpecHandback handback, IReadOnlyList<string> cited, IReadOnlyList<string>? evidence)
    {
        ArgumentNullException.ThrowIfNull(handback);
        ArgumentNullException.ThrowIfNull(cited);
        if (handback.Case != SpecHandbackCase.RequirementsContradictRepository) return new([], null);
        var byId = DerivationEvidence.IndexById(evidence);
        var lines = cited.Where(byId.ContainsKey).Select(id => byId[id]).Distinct().ToList();
        if (cited.Count == 0)
            return new(lines, "a requirements_contradict_repository hand-back cites no evidence. "
                + $"Cite the looks that show the contradiction: {Shape}. If you have not looked, look "
                + "first; a contradiction nobody looked at is not handed back.");
        var unknown = cited.Where(id => !byId.ContainsKey(id)).ToList();
        return unknown.Count == 0
            ? new(lines, null)
            : new(lines, $"the hand-back cites {string.Join(", ", unknown)}, which names no look this "
                + $"derivation took. Cite only ids from your own looks: {Shape}.");
    }
}
