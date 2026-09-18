using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79c: decides which of the checker's findings may STOP a phase. This step can
/// fail an honest phase, so every gate here is biased towards letting the work happen.
/// <para>
/// Three things must hold together before a finding is FALSE. The PREMISE must be one the phase
/// states — resolved against its facts, assumptions and decisions prose and rewritten to the
/// spec's own wording, so a paraphrase is admitted and an invention is not. The VERDICT must be
/// the one that claims a contradiction. The CITATION must be an id the framework minted for a
/// look THIS check took and that reached a verdict (ffa7's rule). Anything else is UNPROVEN and
/// stops nothing.
/// </para>
/// <para>
/// What is deliberately NOT required is that the look returned output. A grep that finds nothing
/// is how an absence is proven, and absence is the commonest way a premise stops holding — so
/// the framework carries the look VERBATIM onto the finding (<see cref="PremiseFinding.Looked"/>)
/// and renders it beside the premise instead. The step from "this look" to "that premise" is a
/// semantic one no framework can decide; making it visible is what a reader needs.
/// </para>
/// <para>
/// One kind is DISCARDED: a VERBATIM restatement of what the phase claims — a done criterion, its
/// goal, one of its steps. That is the accountant's question, asked before and after the work by
/// <see cref="PhaseEntryAccount"/> and VerifyPhase. Only verbatim, and only for the contradiction
/// verdict: a premise that merely shares wording with the goal must still be reportable.
/// </para>
/// </summary>
internal sealed class PremiseCheckAdmission(ILogger logger)
{
    public PremiseCheck Admit(
        PhaseDraft draft, PhasePremises premises, IReadOnlyList<PremiseFinding> answer,
        DerivationLook look)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(premises);
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(look);
        var looks = look.Evidence.Looks.ToDictionary(l => l.Id, l => l, StringComparer.Ordinal);

        var falsified = new List<PremiseFinding>();
        var unproven = new List<PremiseFinding>();
        foreach (var finding in answer)
        {
            if (RestatesAClaim(draft, finding)) continue;
            var stated = PhaseQuotableText.Resolve(premises.Claims, finding.Premise);
            if (stated is null) unproven.Add(NotStated(finding));
            else if (Proven(finding with { Premise = stated }, looks) is { } proven)
                falsified.Add(proven);
            else unproven.Add(Unproven(finding with { Premise = stated }));
        }
        return new PremiseCheck(falsified, unproven);
    }

    private bool RestatesAClaim(PhaseDraft draft, PremiseFinding finding)
    {
        if (!IsContradiction(finding)
            || !PhaseQuotableText.IsVerbatim(PhaseQuotableText.Of(draft), finding.Premise)) return false;
        logger.LogInformation(
            "The premise check quoted what {Phase} CLAIMS rather than what it rests on — that is "
            + "the account's question, not this one; discarding: {Premise}",
            draft.PhaseId, finding.Premise);
        return true;
    }

    private static PremiseFinding? Proven(
        PremiseFinding finding, IReadOnlyDictionary<string, EvidenceLook> looks)
    {
        if (!IsContradiction(finding)) return null;
        var cited = DerivationEvidence.CitationsIn(finding.Cites)
            .Select(id => looks.GetValueOrDefault(id))
            .FirstOrDefault(l => l is { Ran: true });
        return cited is null
            ? null
            : finding with { Evidence = cited.Line, Looked = $"{cited.Repository}: {cited.What}" };
    }

    private static bool IsContradiction(PremiseFinding finding) =>
        string.Equals(
            finding.Verdict?.Trim(), PremiseCheckPrompt.NoLongerHolds, StringComparison.OrdinalIgnoreCase);

    private PremiseFinding NotStated(PremiseFinding finding)
    {
        logger.LogWarning(
            "The premise check reported '{Premise}', which is not a premise this phase states — "
            + "recording it unproven", finding.Premise);
        return finding with { Verdict = PremiseCheckPrompt.Unproven, Evidence = null, Looked = null };
    }

    private PremiseFinding Unproven(PremiseFinding finding)
    {
        logger.LogWarning(
            "The premise check reported '{Premise}' as {Verdict} citing {Cites}, and no look of "
            + "its own that ran minted that id — recording it unproven",
            finding.Premise, finding.Verdict, finding.Cites ?? "nothing");
        return finding with { Verdict = PremiseCheckPrompt.Unproven, Evidence = null, Looked = null };
    }
}
