using AgentSmith.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: decides what of an attack reviewer's answer the framework keeps.
/// <para>
/// A CUT is kept when its quote is a run of the register it names — so the reviser can find the text it
/// replaces — and it rests on a read of its own (<see cref="OwnReadRule"/>): a cut changes text about a
/// line. A FINDING is kept on such a read too, or on a search of its own that ran (grep exits 1 exactly
/// when it found nothing, which is the proof of absence), or on a read of its own of exactly that path
/// that found no file. Everything else is a path a reviewer produced without opening anything, and is
/// dropped and logged. The verdict is the most severe the admitted content supports.
/// </para>
/// </summary>
public sealed class SpecAttackAdmission(ILogger<SpecAttackAdmission> logger)
{
    private const int MissingFileExit = 1;

    internal SpecAttackResult Admit(
        SpecAttackAnswer answer, SpecRegisters registers, DerivationLook? look,
        IReadOnlyDictionary<string, string> names)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var looks = (look?.Evidence.Looks ?? []).ToDictionary(l => l.Id, l => l, StringComparer.Ordinal);
        var reads = OwnReadRule.Reads(look);
        var cuts = answer.Cuts.Select(c => Cut(c, registers, reads, names)).OfType<SpecReviewCut>().ToList();
        var findings = answer.Findings.Select(f => Finding(f, reads, looks, names)).OfType<SpecReviewFinding>().ToList();
        var verdict = answer.Verdict == SpecReviewVerdicts.Refuse && findings.Count > 0 ? SpecReviewVerdicts.Refuse
            : cuts.Count > 0 ? SpecReviewVerdicts.BuildWithCuts
            : SpecReviewVerdicts.Build;
        if (verdict != answer.Verdict)
            logger.LogWarning("Attack review answered {Said}; stored as {Verdict}, what its admitted content supports",
                answer.Verdict, verdict);
        return new SpecAttackResult(verdict, cuts, findings);
    }

    private SpecReviewCut? Cut(
        SpecAttackItem cut, SpecRegisters registers, IReadOnlyDictionary<string, EvidenceLook> reads,
        IReadOnlyDictionary<string, string> names)
    {
        if (!SpecRegisterNames.All.Contains(cut.Register ?? string.Empty, StringComparer.Ordinal)
            || !registers.Holds(cut.Register!, cut.Quote))
            return Discard<SpecReviewCut>("cut", cut, $"its quote is not a run of the register '{cut.Register}'");
        return ReadEvidence(cut, reads, names) is { } evidence
            ? new SpecReviewCut(cut.Register!, cut.Quote!, cut.Replacement, evidence)
            : Discard<SpecReviewCut>("cut", cut, $"no read of its own that returned that line minted {cut.Cites ?? "nothing"}");
    }

    private SpecReviewFinding? Finding(
        SpecAttackItem finding, IReadOnlyDictionary<string, EvidenceLook> reads,
        IReadOnlyDictionary<string, EvidenceLook> looks, IReadOnlyDictionary<string, string> names)
    {
        if (string.IsNullOrWhiteSpace(finding.Text)) return Discard<SpecReviewFinding>("finding", finding, "it says nothing");
        var evidence = ReadEvidence(finding, reads, names) ?? AbsenceEvidence(finding, looks, names);
        return evidence is null
            ? Discard<SpecReviewFinding>("finding", finding, $"no look of its own that proves it minted {finding.Cites ?? "nothing"}")
            : new SpecReviewFinding(finding.Text.Trim(), evidence);
    }

    private static string? ReadEvidence(
        SpecAttackItem item, IReadOnlyDictionary<string, EvidenceLook> reads, IReadOnlyDictionary<string, string> names) =>
        OwnReadRule.Read(item.Cites, item.Repository, item.Path ?? string.Empty, reads) is { } read
        && OwnReadRule.Within(item.Line, read)
            ? $"{Name(read, names)}/{OwnReadRule.Normalize(read.Path)}:{item.Line} [{read.Id}]"
            : null;

    // A search that reached a verdict proves presence or absence; a read of exactly the finding's path
    // that found no file proves that file is absent.
    private static string? AbsenceEvidence(
        SpecAttackItem item, IReadOnlyDictionary<string, EvidenceLook> looks, IReadOnlyDictionary<string, string> names)
    {
        foreach (var id in DerivationEvidence.CitationsIn(item.Cites))
        {
            if (!looks.TryGetValue(id, out var look) || !look.Ran) continue;
            if (look.Kind == EvidenceRecord.Search)
                return $"{Name(look, names)} search:'{Pattern(look.What)}' [{look.Id}]";
            if (look.Kind == EvidenceRecord.Read && look.ExitCode == MissingFileExit
                && OwnReadRule.Same(look, item.Repository, item.Path))
                return $"{Name(look, names)}/{OwnReadRule.Normalize(look.Path)} [{look.Id}]";
        }
        return null;
    }

    private static string Name(EvidenceLook look, IReadOnlyDictionary<string, string> names) =>
        names.GetValueOrDefault(look.Repository) ?? look.Repository;

    // The search's minted line spells "grep -E '<pattern>' <path>" or "rg … '<pattern>' <path>".
    private static string Pattern(string what)
    {
        var open = what.IndexOf('\'');
        var close = what.LastIndexOf('\'');
        return open >= 0 && close > open ? what[(open + 1)..close] : what;
    }

    private T? Discard<T>(string kind, SpecAttackItem item, string why) where T : class
    {
        logger.LogWarning("Attack review {Kind} on {Repo}/{Path}:{Line} discarded — {Why}",
            kind, item.Repository, item.Path, item.Line, why);
        return null;
    }
}
