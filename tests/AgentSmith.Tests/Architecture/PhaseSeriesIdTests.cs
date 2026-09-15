using System.Text.RegularExpressions;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-09-03-2f81: phases cut from one piece of work share a minted number and differ by
/// a trailing letter, so a file listing shows the kinship a shared date does not.
/// <para>
/// The danger this pins is not refusal, it is SILENCE. A letter-suffixed file that a
/// reader cannot parse is not rejected — it falls out of the date-minted match, lands in
/// the closed counter namespace the naming rules deliberately do not judge, and every
/// rule keyed on that recognition stops applying while the suite stays green. Sixteen
/// such files were written once and only the schema test noticed.
/// </para>
/// </summary>
public sealed class PhaseSeriesIdTests
{
    /// <summary>
    /// The date-minted shape as it is SPELLED in source — a regex fragment, in either the
    /// backslash-d or the bracket spelling, with the JSON files carrying a doubled
    /// backslash. The run id is not matched and needs no exclusion: its four-digit year is
    /// followed by a time, so the tail this fragment needs — a four-digit group, two
    /// two-digit groups, then the hex — never occurs in it.
    /// </summary>
    private const string Spelled =
        @"(?:\\{1,2}d|\[0-9\])\{4\}-(?:\\{1,2}d|\[0-9\])\{2\}-(?:\\{1,2}d|\[0-9\])\{2\}-\[0-9a-f\]\{4\}";

    private const string SeriesLetter = "[a-z]?";

    /// <summary>
    /// Every place in this repository that decides whether a string IS a date-minted phase
    /// id. Listed rather than discovered, because a reader that stops matching this
    /// fragment has not been fixed — it has been renamed or rewritten, and the empty-match
    /// assertion below is what says so.
    /// </summary>
    private static readonly string[] Readers =
    [
        ".agentsmith/phase-spec.schema.json",
        ".agentsmith/decision.schema.json",
        ".claude/hooks/phase-gate.sh",
        "src/backend/AgentSmith.Application/Services/SpecDialog/RequiresEdgeChecker.cs",
        "src/backend/AgentSmith.Infrastructure.Core/Services/DecisionFileLabel.cs",
        "src/backend/AgentSmith.Server/Services/Events/RunStepsReader.cs",
        "src/dashboard/src/lib/runStepRail.ts",
        "tests/AgentSmith.Tests/Architecture/PhaseIdReader.cs",
        "tests/AgentSmith.Tests/Architecture/PhaseIdSchemaTests.cs",
        "tests/AgentSmith.Tests/Architecture/PhaseSpecFile.cs",
        "tests/AgentSmith.Tests/Skills/DesignPartnerPlaceholderTests.cs",
    ];

    [Fact]
    public void DateMintedShape_EveryReaderInThisRepository_AdmitsASeriesLetter()
    {
        var deaf = new List<string>();
        foreach (var relative in Readers)
        {
            var text = File.ReadAllText(
                Path.Combine(ArchitectureSources.RepositoryRoot, relative));
            var spellings = Regex.Matches(text, Spelled);

            spellings.Should().NotBeEmpty(
                $"{relative} is listed here as a reader of a date-minted id — if it no "
                + "longer spells one, this list is out of date, which is the failure mode "
                + "the list exists to catch");

            deaf.AddRange(spellings
                .Where(match => !text[(match.Index + match.Length)..]
                    .StartsWith(SeriesLetter, StringComparison.Ordinal))
                .Select(match => $"{relative}: {match.Value}"));
        }

        deaf.Should().BeEmpty(
            "a reader that does not admit the series letter does not REFUSE a series id — "
            + "it stops recognising it as a phase id at all, and the rules keyed on that "
            + "recognition go quiet.\n  " + string.Join("\n  ", deaf));
    }

    [Fact]
    public void FileStem_ASeriesMember_ReadsItsOwnIdAndItsOwnSlug()
    {
        var file = PhaseSpecFile.ForStem("2026-08-24-8a3fb-the-second-slice");

        file.PhaseId.Should().Be("2026-08-24-8a3fb");
        file.Slug.Should().Be("the-second-slice");
        file.IsDateMinted.Should().BeTrue("a series member is judged by every naming rule");
    }

    /// <summary>
    /// The reason the letter is appended rather than dashed. This repository's own example
    /// slug begins with a one-letter word, so in a dashed form no reader could tell the
    /// two apart — and the phase whose id ends at the hex would silently gain a member.
    /// </summary>
    [Fact]
    public void FileStem_ASlugBeginningWithAOneLetterWord_IsNotReadAsASeries()
    {
        var file = PhaseSpecFile.ForStem("2026-08-24-8a3f-a-phase-id-can-be-minted-offline");

        file.PhaseId.Should().Be("2026-08-24-8a3f");
        file.Slug.Should().Be("a-phase-id-can-be-minted-offline");
    }

    [Fact]
    public void FileStem_TwoMembersOfOneSeries_AreDistinctIds()
    {
        var first = PhaseSpecFile.ForStem("2026-08-24-8a3fa-the-first-slice");
        var second = PhaseSpecFile.ForStem("2026-08-24-8a3fb-the-second-slice");

        first.PhaseId.Should().NotBe(second.PhaseId,
            "a prefix read that stopped at the hex would collapse a whole series onto one "
            + "id: requires-edges become self-edges and decision files fall together");
    }
}
