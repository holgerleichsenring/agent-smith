using System.Text.RegularExpressions;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Tests.Prompts;
using AgentSmith.Tests.Validation;
using FluentAssertions;
using static AgentSmith.Tests.Skills.TicketGraphSkillSentences;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// The rules of series 2026-10-14-dbce — ticket lists in merge order and absence criteria judged over the source
/// tree — read from the PINNED catalog. The parser, checker, review and filing work without these sentences; a
/// release that drops one fails here instead of leaving a design partner that never proposes a list.
/// </summary>
public sealed partial class TicketGraphSkillRulesTests
{
    /// <summary>First agent-smith-skills release carrying every rule of the series.</summary>
    private static readonly Version TicketGraphRelease = new(5, 18, 0);

    [GeneratedRegex("```outcome\\s*\\n(kind: tickets\\n.*?)```", RegexOptions.Singleline)]
    private static partial Regex TicketsBlock();

    private static string Because(string rule) =>
        $"the pin is {PackagedMaster.Pin} and {rule} ships from v{TicketGraphRelease}";

    private static string DesignPartner() => PackagedMaster.Read("design-partner-master");

    private static string TicketsFormat() => PackagedMaster.Reference("design-outcome-tickets");

    private static string Block() => TicketsBlock().Match(TicketsFormat()).Groups[1].Value;

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_OffersTicketsAtMergeBoundaries() =>
        DesignPartner().Should().Contain(MergeBoundaries, Because("the tickets outcome"));

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_AsksTheMergeTestThenTheBranchStateTest()
    {
        var master = DesignPartner();

        master.Should().Contain(MergeTestFirst, Because("the merge test"));
        master.Should().Contain("Then apply the branch-state test:", Because("the branch-state test second"));
        master.Should().NotContain("Apply the branch-state test first:", "the merge test is asked first");
    }

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_TicketIsABoundedQuotedEnglishTextWithAUniqueKey() =>
        TicketsFormat().Should().Contain(TicketText, Because("the ticket text rule"));

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_RequiresNameOnlyTicketsMergedFirstWithIdsWithoutHash() =>
        TicketsFormat().Should().Contain(MergeOrder, Because("the merge order rule"));

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_SplitsMoreThanTwentyTicketsAcrossProposals()
    {
        TicketsFormat().Should().Contain(ProposalSize, Because("the proposal size rule"));
    }

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_TicketIsWrittenAgainstItsMergedRequiresAndSizedForOneRun() =>
        TicketsFormat().Should().Contain(SizedForOneRun, Because("the ticket size rule"));

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_ProposesTicketsOnlyUnbound() =>
        TicketsFormat().Should().Contain(UnboundOnly, Because("the unbound-only rule"));

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_ScopesAbsenceCriteriaToTheSourceTree() =>
        PackagedMaster.Reference("design-outcome-spec").Should().Contain(AbsenceScoped, Because("the scoped absence criterion"));

    [Fact]
    public void EmbeddedSkills_CodingAgentMaster_JudgesAbsenceOverTheSourceTree() =>
        PackagedMaster.Read("coding-agent-master").Should().Contain(AbsenceJudged, Because("the absence judgement"));

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_SaysWhatATicketListFiles() =>
        TicketsFormat().Should().Contain(WhatTicketsFile, Because("the filing sentence"));

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_ShowsATicketTextBlockWithoutSpecs()
    {
        var block = Block();

        block.Should().NotBeEmpty(Because("the kind: tickets block"));
        foreach (var field in new[] { "key:", "title:", "requirement:", "done:", "requires:", "repos:", "\"ticket:1234\"" })
            block.Should().Contain(field);
        foreach (var field in new[] { "spec:", "work:", "steps:", "facts:" })
            block.Should().NotContain(field, "a ticket text carries no spec");

        var values = block.Split('\n').Select(line => line.Trim().TrimStart('-').Trim())
            .Where(line => line.Length > 0 && line is not ("kind: tickets" or "tickets:" or "done:"));
        values.Should().OnlyContain(line => line.StartsWith('"') || Regex.IsMatch(line, "^[a-z]+: [\"\\[]"),
            "every string of a ticket text is double-quoted");
    }

    [Fact]
    public void EmbeddedSkills_DesignPartnerMaster_ShowsAnExampleTheCheckerAccepts()
    {
        var map = OutcomeYamlReader.ReadMap(Block());

        var resolution = TicketGraphFixtures.Parser().Parse(map, ["backend"]);

        resolution.Should().BeOfType<OutcomeResolved>()
            .Which.Proposal.Should().BeOfType<TicketGraphOutcome>().Which.Tickets.Should().HaveCount(2);
    }
}
