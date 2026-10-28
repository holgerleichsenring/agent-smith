using System.Text.RegularExpressions;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Tests.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// 2026-10-28-7a69d: the comment outcome, read from the PINNED catalog. The parser and the approval path
/// work without these sentences; a release that drops them leaves a design partner that never proposes one,
/// and a block whose field the parser does not read fails here rather than in a person's conversation.
/// </summary>
public sealed partial class TicketCommentSkillRulesTests
{
    /// <summary>First agent-smith-skills release carrying the comment outcome.</summary>
    private static readonly Version CommentRelease = new(5, 22, 0);

    [GeneratedRegex("```outcome\\s*\\n(kind: comment\\n.*?)```", RegexOptions.Singleline)]
    private static partial Regex CommentBlock();

    private static string Because(string rule) => $"the pin is {PackagedMaster.Pin} and {rule} ships from v{CommentRelease}";

    [Fact]
    public void EmbeddedCatalog_DesignPartner_DocumentsTheCommentOutcome()
    {
        var master = PackagedMaster.Read("design-partner-master");

        master.Should().Contain("### A conversation bound to a ticket", Because("the bound-ticket review"));
        master.Should().Contain("is an **answer** that says whether it holds", Because("judging the ticket first"));
        master.Should().Contain("posts the comment on the ticket and starts nothing", Because("the comment outcome"));
    }

    [Fact]
    public void EmbeddedCatalog_DesignPartnerCommentBlock_IsWhatTheParserReads()
    {
        var block = CommentBlock().Match(PackagedMaster.Read("design-partner-master")).Groups[1].Value;

        new TicketCommentOutcomeParser().Parse(OutcomeYamlReader.ReadMap(block))
            .Should().BeOfType<OutcomeResolved>(Because("the comment block"))
            .Which.Proposal.Should().BeOfType<TicketCommentOutcome>();
    }
}
