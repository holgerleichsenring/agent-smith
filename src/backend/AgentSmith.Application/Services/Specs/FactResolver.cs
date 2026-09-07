using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: decides IN CODE which of the derivation's fact lines are facts.
/// <para>
/// A line is a fact when the id it cites is one the framework minted for a look this
/// derivation took; a line citing nothing, or an id nobody minted, is an assumption. The
/// model never labels its own lines — that is self-certification, the failure the
/// delivery account was built to end. <see cref="CitationResolver"/> is not reused: it
/// resolves an account ROW by command prefix, and a fact is resolved by id alone.
/// </para>
/// <para>
/// No mechanism here judges whether the derivation asked the RIGHT question of the
/// code. A reader who sees "7 high, 1 direct" beside the audit that said so can ask
/// "which?"; a reader who sees nothing cannot.
/// </para>
/// </summary>
public sealed class FactResolver
{
    public PhaseFacts Resolve(
        IReadOnlyList<FactLine> lines, IReadOnlyList<string>? evidence)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var byId = IndexById(evidence ?? []);
        var facts = new List<PhaseFact>();
        var assumptions = new List<string>();
        foreach (var line in lines)
        {
            if (line.Claim.Length == 0) continue;
            if (byId.TryGetValue(DerivationEvidence.NormalizeCitation(line.Cites), out var minted))
                facts.Add(new PhaseFact(line.Claim, minted));
            else
                assumptions.Add(line.Claim);
        }
        return new PhaseFacts(facts, assumptions);
    }

    private static Dictionary<string, string> IndexById(IReadOnlyList<string> evidence)
    {
        var byId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in evidence)
            if (DerivationEvidence.IdOf(line) is { } id)
                byId.TryAdd(id, line);
        return byId;
    }
}
