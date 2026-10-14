using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762k: what of the series-contract reviewer's answer the framework keeps — 8762e's rule, applied to
/// the member a cut names. A cut naming no member is dropped; its quote must be a run of the register it names in
/// that member. In a run its evidence is a read of its own (<see cref="SpecAttackAdmission"/>); in a design turn,
/// where the drafts exist only in the proposal, it is the contract entry it breaks, quoted verbatim. A finding
/// rests on the same evidence. Every dropped cut is an evidence problem of the round.
/// </summary>
public sealed class SeriesContractAdmission(SpecAttackAdmission attack, ILogger<SeriesContractAdmission> logger)
{
    private const int EntryChars = 12;

    internal SeriesContractReview Admit(SeriesContractAnswer answer, SeriesContractInput input, DerivationLook? look)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(input);
        var lines = SeriesContractPromptSection.Lines(input.Contract);
        var dropped = new List<SpecEvidenceProblem>();
        var cuts = new List<SpecReviewCut>();
        foreach (var cut in answer.Cuts)
        {
            var why = Cut(cut, input, lines, look, out var admitted);
            if (admitted is not null) cuts.Add(admitted);
            else dropped.Add(new SpecEvidenceProblem($"series cut on {cut.SpecId ?? "no member"}: \"{cut.Item.Quote}\"", why!));
        }
        var findings = answer.Findings.Select(f => Finding(f, input, lines, look)).OfType<SpecReviewFinding>().ToList();
        foreach (var problem in dropped) logger.LogWarning("Series contract cut discarded — {Claim}: {Why}", problem.Claim, problem.Problem);
        var verdict = answer.Verdict == SpecReviewVerdicts.Refuse && findings.Count > 0 ? SpecReviewVerdicts.Refuse
            : cuts.Count > 0 ? SpecReviewVerdicts.BuildWithCuts
            : SpecReviewVerdicts.Build;
        return new SeriesContractReview(new SpecAttackResult(verdict, cuts, findings), dropped);
    }

    private string? Cut(
        SeriesContractItem cut, SeriesContractInput input, IReadOnlyList<SeriesContractLine> lines, DerivationLook? look,
        out SpecReviewCut? admitted)
    {
        admitted = null;
        var member = input.Members.FirstOrDefault(m => m.Draft.SpecId == cut.SpecId);
        if (member is null) return "it names no member of the series";
        var register = cut.Item.Register ?? string.Empty;
        var registers = SpecRegisters.Read(member.Draft.Yaml);
        if (!SpecRegisterNames.All.Contains(register, StringComparer.Ordinal) || !registers.Holds(register, cut.Item.Quote))
            return $"its quote is not a run of the register '{register}' of {member.Draft.SpecId}";
        admitted = input.InRun
            ? attack.Admit(new SpecAttackAnswer(SpecReviewVerdicts.BuildWithCuts, [cut.Item], []), registers, look,
                input.RepositoryNames).Cuts.FirstOrDefault()
            : Entry(cut.Entry, lines) is { } entry ? new SpecReviewCut(register, cut.Item.Quote!, cut.Item.Replacement, entry) : null;
        admitted = admitted is null ? null : admitted with { SpecId = member.Draft.SpecId };
        return admitted is not null ? null
            : input.InRun ? $"no read of its own that returned that line minted {cut.Item.Cites ?? "nothing"}"
            : "it quotes no entry of the series contract verbatim";
    }

    private SpecReviewFinding? Finding(
        SeriesContractItem finding, SeriesContractInput input, IReadOnlyList<SeriesContractLine> lines, DerivationLook? look)
    {
        if (!input.InRun && !string.IsNullOrWhiteSpace(finding.Item.Text) && Entry(finding.Entry, lines) is { } entry)
            return new SpecReviewFinding(finding.Item.Text.Trim(), entry);
        return attack.Admit(new SpecAttackAnswer(SpecReviewVerdicts.Build, [], [finding.Item]), SpecRegisters.Read(string.Empty),
            look, input.RepositoryNames).Findings.FirstOrDefault();
    }

    /// <summary>The contract line the quoted entry is verbatim part of, as stored evidence; null when none.</summary>
    internal static string? Entry(string? quoted, IReadOnlyList<SeriesContractLine> lines)
    {
        var text = quoted?.Trim().TrimStart('-', ' ');
        if (text is null || text.Length < EntryChars) return null;
        return lines.FirstOrDefault(line => line.Text.Contains(text, StringComparison.Ordinal)) is { } match
            ? $"series contract: {match.Text}"
            : null;
    }
}
