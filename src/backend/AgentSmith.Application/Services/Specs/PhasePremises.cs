using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79c: what a phase SAYS it rests on — the three things the check is put to,
/// and the corpus a reported premise has to resolve against.
/// <para>
/// Facts and assumptions are read off the draft. Decisions are not on the draft at all and are
/// re-parsed out of its own yaml, the way <c>PhaseExecutionPromptBlocks.DoneCriteria</c> re-reads
/// the done-list, and handed over AS PROSE. Collecting "the paths a decision names" would need a
/// token shape nothing in the product defines, so the checker reads the prose and looks where it
/// wants; <see cref="Claims"/> and the admission rule are what keep its ANSWER honest.
/// </para>
/// <para>
/// The deriver's own machine-written decision is excluded (<see cref="PhasePremisesReader"/>):
/// it is boilerplate every derived phase carries, and it points at a RUN ARTIFACT that exists in
/// no sandbox — a checker asked whether it still holds would look for that file, not find it,
/// and fail an honest phase.
/// </para>
/// </summary>
public sealed record PhasePremises(
    IReadOnlyList<PhaseFact> Facts,
    IReadOnlyList<string> Assumptions,
    string Decisions,
    IReadOnlyList<string> Claims)
{
    /// <summary>Nothing to check: a phase that states no premise of its own. A derived phase
    /// whose only decision is the deriver's boilerplate lands here, and costs no call.</summary>
    public bool None => Claims.Count == 0;

    public static PhasePremises For(PhaseDraft draft) => PhasePremisesReader.Read(draft);

    /// <summary>The three lists as the checker is shown them, each headed by what it is.</summary>
    public string Render()
    {
        var sections = new List<string>();
        if (Facts.Count > 0)
            sections.Add("FACTS — each one was read somewhere when the phase was written.\n"
                + string.Join("\n", Facts.Select(f =>
                    $"- {f.Claim}" + Where(f.Evidence))));
        if (Assumptions.Count > 0)
            sections.Add("ASSUMPTIONS — stated without a look behind them.\n"
                + string.Join("\n", Assumptions.Select(a => $"- {a}")));
        if (Decisions.Length > 0)
            sections.Add("DECISIONS — prose. A claim about the code may be stated anywhere in it.\n"
                + Decisions);
        return string.Join("\n\n", sections);
    }

    // A fact's stored evidence was minted by the DERIVATION, under the derivation's letter. The
    // reader is taught its own letter and may cite only ids the framework minted for it here, so
    // a foreign id is stripped rather than shown: an id in the prompt that resolves to nothing is
    // an invitation to cite it.
    private static string Where(string? evidence)
    {
        var text = Regex.Replace(evidence ?? string.Empty, @"^\s*\[[A-Za-z]+\d+\]\s*", string.Empty).Trim();
        return text.Length == 0 ? string.Empty : $"\n  (where it was seen, when the phase was written: {text})";
    }
}
