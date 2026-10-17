using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: decides IN CODE which of the derivation's fact lines are facts.
/// <para>
/// A line is a fact when an id it cites is one the framework minted for a look this
/// derivation took and that look reached a verdict; a line citing nothing, or only ids nobody
/// minted, is an assumption. The model never labels its own lines — that is self-certification,
/// the failure the delivery account was built to end. <see cref="CitationResolver"/> is not
/// reused: it resolves an account ROW by command prefix, and a fact is resolved by id alone.
/// </para>
/// <para>
/// 2026-10-17-293bc: a line whose cited looks all COULD NOT RUN is unsettled. Such a look proves
/// nothing about the code — an unrestored project's audit says so in its own words — so a claim
/// resting on it is neither a fact nor a premise: the premise check reads facts and assumptions
/// only, and a tool that runs later is not the code contradicting the spec. Each cited id is
/// resolved on its own, so "L1, L2" is a fact when either look ran.
/// </para>
/// <para>
/// No mechanism here judges whether the derivation asked the RIGHT question of the
/// code. A reader who sees "7 high, 1 direct" beside the audit that said so can ask
/// "which?"; a reader who sees nothing cannot.
/// </para>
/// </summary>
public sealed class FactResolver
{
    public SpecFacts Resolve(
        IReadOnlyList<FactLine> lines, IReadOnlyList<EvidenceLook>? looks)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var byId = (looks ?? []).GroupBy(l => l.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var facts = new List<SpecFact>();
        var unsettled = new List<SpecFact>();
        var assumptions = new List<string>();
        foreach (var line in lines.Where(l => l.Claim.Length > 0))
        {
            var cited = DerivationEvidence.CitationsIn(line.Cites)
                .Where(byId.ContainsKey).Select(id => byId[id]).ToList();
            if (cited.FirstOrDefault(l => l.Ran) is { } ran) facts.Add(new SpecFact(line.Claim, ran.Line));
            else if (cited.Count > 0) unsettled.Add(new SpecFact(line.Claim, cited[0].Line));
            else assumptions.Add(line.Claim);
        }
        return new SpecFacts(facts, assumptions, unsettled);
    }
}
