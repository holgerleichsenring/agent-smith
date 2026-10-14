using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762f: the plugin's grep after a revision. Every cut's quote is searched as a word run in
/// each of the seven registers of the revised spec; every fact with an evidence problem whose claim and
/// evidence still stand is reported too. A cut whose replacement holds its own quote removes nothing and
/// is skipped; a fact holding a facts cut's replacement was re-cited. Over a refused revision — the input
/// draft unchanged — every cut still stands, with no special case.
/// </summary>
public sealed class SpecCutResidues
{
    public IReadOnlyList<SpecReviewResidue> Of(
        SpecDraft revised, IReadOnlyList<SpecReviewCut> cuts, IReadOnlyList<SpecEvidenceProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(revised);
        ArgumentNullException.ThrowIfNull(cuts);
        ArgumentNullException.ThrowIfNull(problems);
        var registers = SpecRegisters.Read(revised.Yaml);
        var residues = new List<SpecReviewResidue>();
        foreach (var cut in cuts.Where(cut => !SpecQuotableText.IsRunIn(cut.Replacement, cut.Quote)))
            residues.AddRange(SpecRegisterNames.All
                .Where(register => registers.Holds(register, cut.Quote))
                .Select(register => new SpecReviewResidue(register, cut.Quote)));

        var recitations = cuts
            .Where(cut => cut.Register == SpecRegisterNames.Facts && !string.IsNullOrWhiteSpace(cut.Replacement))
            .Select(cut => cut.Replacement!)
            .ToList();
        residues.AddRange(revised.Facts
            .Where(fact => problems.Any(problem => SpecQuotableText.IsVerbatim([fact.Claim], problem.Claim)))
            .Where(fact => !recitations.Any(replacement =>
                SpecRevisionEdit.HoldsRun(fact.Claim, replacement) || SpecRevisionEdit.HoldsRun(fact.Evidence, replacement)))
            .Select(fact => new SpecReviewResidue(SpecRegisterNames.Facts, fact.Claim)));
        return [.. residues.Distinct()];
    }
}
